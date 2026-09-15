using System.Diagnostics;
using System.Text.Json;
using CadastroAlunos.Models;
using CadastroAlunos.Services;
using Microsoft.AspNetCore.Mvc;

namespace CadastroAlunos.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDocumentVerificationService _verificationService;
    private readonly IFirebaseService _firebaseService;
    private readonly IHttpClientFactory _httpClientFactory;

    private const long MaxFileSizeLimit = 5 * 1024 * 1024; // 5 Megabytes em bytes

    public HomeController(
        ILogger<HomeController> logger,
        IDocumentVerificationService verificationService,
        IFirebaseService firebaseService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _verificationService = verificationService;
        _firebaseService = firebaseService;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new AlunoCadastroViewModel());
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AlunoCadastroViewModel model)
    {
        // 1. Validação do Documento Comprobatório (RG)
        if (model.Documento == null || model.Documento.Length == 0)
        {
            ModelState.AddModelError("Documento", "É obrigatório anexar uma foto ou arquivo PDF do seu RG.");
        }
        else
        {
            // Restrição estrita de 5MB
            if (model.Documento.Length > MaxFileSizeLimit)
            {
                var tamanhoEmMb = model.Documento.Length / (1024.0 * 1024.0);
                ModelState.AddModelError("Documento", 
                    $"O arquivo selecionado possui {tamanhoEmMb:N1} MB. O tamanho máximo permitido pelo sistema é de 5 MB. Por favor, envie um arquivo mais leve.");
            }

            // Restrição de Formato (PDF ou PNG, aceitando também JPG comum de câmeras de celular)
            var extensao = Path.GetExtension(model.Documento.FileName).ToLowerInvariant();
            var extensoesPermitidas = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
            if (!extensoesPermitidas.Contains(extensao))
            {
                ModelState.AddModelError("Documento", 
                    "Formato de arquivo não suportado. Por favor, envie seu RG em formato PDF ou imagem PNG.");
            }
        }

        // Validação da filiação paterna
        if (!model.PaiNaoDeclarado && string.IsNullOrWhiteSpace(model.NomeDoPai))
        {
            ModelState.AddModelError("NomeDoPai", 
                "Por favor, informe o nome do pai ou marque a opção 'Pai não declarado na certidão / RG'.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // 2. Processa e verifica se o documento pertence à pessoa
            var resultadoValidacao = await _verificationService.VerificarDocumentoAsync(model, model.Documento!);

            // 3. Grava os dados cadastrais e o documento no Firebase
            var (registroId, docUrl) = await _firebaseService.SalvarAlunoEDocumentoAsync(
                model, 
                resultadoValidacao, 
                model.Documento!);

            resultadoValidacao.FirebaseRegistroId = registroId;
            resultadoValidacao.DocumentoUrlOuCaminho = docUrl;

            // 4. Retorna a tela com o resultado claro da validação
            return View("Resultado", resultadoValidacao);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o cadastro e gravação do aluno {Nome}", model.NomeCompleto);
            ModelState.AddModelError(string.Empty, 
                "Ocorreu uma falha ao salvar seus dados e conferir o documento. Por favor, tente novamente ou fale com a secretaria.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Resultado(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction(nameof(Index));
        }

        var registro = await _firebaseService.ObterRegistroPorIdAsync(id);
        if (registro == null)
        {
            TempData["MensagemErro"] = "Cadastro não localizado.";
            return RedirectToAction(nameof(Index));
        }

        var resultado = new ResultadoValidacao
        {
            IsValido = registro.DocumentoValido,
            PontuacaoConfianca = registro.PontuacaoConfianca,
            TituloParecer = registro.ResumoValidacao,
            MensagemPrincipal = registro.DocumentoValido
                ? $"Cadastro do aluno {registro.NomeCompleto} localizado. O documento comprobatório foi validado com sucesso."
                : $"Atenção: O documento do aluno {registro.NomeCompleto} apresentou divergências e precisa ser revisado.",
            DetalhesConferencia = registro.DetalhesValidacao,
            Alertas = registro.AlertasValidacao,
            DocumentoNomeArquivo = registro.DocumentoNomeOriginal,
            DocumentoTamanhoBytes = registro.DocumentoTamanhoBytes,
            DocumentoUrlOuCaminho = registro.DocumentoUrl,
            FirebaseRegistroId = registro.Id,
            DataProcessamento = registro.CriadoEm.ToLocalTime(),
            Aluno = new AlunoCadastroViewModel
            {
                NomeCompleto = registro.NomeCompleto,
                NomeDaMae = registro.NomeDaMae,
                NomeDoPai = registro.NomeDoPai,
                PaiNaoDeclarado = registro.PaiNaoDeclarado,
                NumeroRg = registro.NumeroRg,
                Cep = registro.Cep,
                Logradouro = registro.Logradouro,
                Numero = registro.Numero,
                Complemento = registro.Complemento,
                Bairro = registro.Bairro,
                Cidade = registro.Cidade,
                Estado = registro.Estado
            }
        };

        return View(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Alunos()
    {
        var lista = await _firebaseService.ListarRegistrosAsync();
        return View(lista);
    }

    [HttpGet]
    public IActionResult Guia()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> BuscarCep(string cep)
    {
        if (string.IsNullOrWhiteSpace(cep))
            return BadRequest(new { erro = "CEP vazio" });

        var cepLimpo = new string(cep.Where(char.IsDigit).ToArray());
        if (cepLimpo.Length != 8)
            return BadRequest(new { erro = "CEP deve ter 8 dígitos" });

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"https://viacep.com.br/ws/{cepLimpo}/json/");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao consultar ViaCEP para o CEP {Cep}", cepLimpo);
        }

        return NotFound(new { erro = "CEP não encontrado" });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
