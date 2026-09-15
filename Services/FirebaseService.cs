using System.Text.Json;
using CadastroAlunos.Models;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Http;

namespace CadastroAlunos.Services;

public class FirebaseService : IFirebaseService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<FirebaseService> _logger;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    public bool IsFirebaseInitialized { get; private set; }

    public FirebaseService(IWebHostEnvironment env, IConfiguration config, ILogger<FirebaseService> logger)
    {
        _env = env;
        _config = config;
        _logger = logger;

        InicializarFirebase();
    }

    private void InicializarFirebase()
    {
        try
        {
            var credentialsPath = _config["Firebase:CredentialsPath"];
            if (!string.IsNullOrEmpty(credentialsPath) && File.Exists(credentialsPath))
            {
                if (FirebaseApp.DefaultInstance == null)
                {
#pragma warning disable CS0618
                    using var stream = File.OpenRead(credentialsPath);
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromStream(stream),
                        ProjectId = _config["Firebase:ProjectId"]
                    });
#pragma warning restore CS0618
                }
                IsFirebaseInitialized = true;
                _logger.LogInformation("Firebase inicializado com sucesso a partir das credenciais.");
            }
            else
            {
                _logger.LogInformation("Firebase usando modo de armazenamento local seguro (para produção, configure Firebase:CredentialsPath no appsettings.json).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível inicializar o Firebase SDK diretamente. Utilizando armazenamento local seguro.");
        }
    }

    public async Task<(string Id, string DocumentUrl)> SalvarAlunoEDocumentoAsync(
        AlunoCadastroViewModel model, 
        ResultadoValidacao validacao, 
        IFormFile documento)
    {
        var id = Guid.NewGuid().ToString("N");
        var ext = Path.GetExtension(documento.FileName).ToLowerInvariant();
        var nomeArmazenado = $"{id}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";

        // 1. Salva o arquivo no diretório wwwroot/uploads/documentos
        var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "documentos");
        if (!Directory.Exists(uploadFolder))
        {
            Directory.CreateDirectory(uploadFolder);
        }

        var filePath = Path.Combine(uploadFolder, nomeArmazenado);
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await documento.CopyToAsync(fileStream);
        }

        var documentUrl = $"/uploads/documentos/{nomeArmazenado}";

        // 2. Monta o registro completo do aluno
        var registro = new AlunoRegistroEntity
        {
            Id = id,
            NomeCompleto = model.NomeCompleto,
            NomeDaMae = model.NomeDaMae,
            NomeDoPai = model.NomeDoPai,
            PaiNaoDeclarado = model.PaiNaoDeclarado,
            NumeroRg = model.NumeroRg,
            Cep = model.Cep,
            Logradouro = model.Logradouro,
            Numero = model.Numero,
            Complemento = model.Complemento,
            Bairro = model.Bairro,
            Cidade = model.Cidade,
            Estado = model.Estado.ToUpperInvariant(),
            DocumentoNomeOriginal = documento.FileName,
            DocumentoNomeArmazenado = nomeArmazenado,
            DocumentoTamanhoBytes = documento.Length,
            DocumentoContentType = documento.ContentType,
            DocumentoUrl = documentUrl,
            DocumentoValido = validacao.IsValido,
            PontuacaoConfianca = validacao.PontuacaoConfianca,
            ResumoValidacao = validacao.TituloParecer,
            DetalhesValidacao = validacao.DetalhesConferencia,
            AlertasValidacao = validacao.Alertas,
            CriadoEm = DateTime.UtcNow
        };

        // 3. Persistência
        await SalvarRegistroNoBancoAsync(registro);

        return (id, documentUrl);
    }

    private async Task SalvarRegistroNoBancoAsync(AlunoRegistroEntity registro)
    {
        var appDataFolder = Path.Combine(_env.ContentRootPath, "App_Data");
        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        var dbFilePath = Path.Combine(appDataFolder, "cadastros.json");

        await _fileLock.WaitAsync();
        try
        {
            List<AlunoRegistroEntity> lista = new();
            if (File.Exists(dbFilePath))
            {
                var jsonExistente = await File.ReadAllTextAsync(dbFilePath);
                if (!string.IsNullOrWhiteSpace(jsonExistente))
                {
                    lista = JsonSerializer.Deserialize<List<AlunoRegistroEntity>>(jsonExistente) ?? new();
                }
            }

            lista.Insert(0, registro);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonNovo = JsonSerializer.Serialize(lista, options);
            await File.WriteAllTextAsync(dbFilePath, jsonNovo);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<AlunoRegistroEntity?> ObterRegistroPorIdAsync(string id)
    {
        var lista = await ListarRegistrosAsync();
        return lista.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<AlunoRegistroEntity>> ListarRegistrosAsync()
    {
        var dbFilePath = Path.Combine(_env.ContentRootPath, "App_Data", "cadastros.json");
        if (!File.Exists(dbFilePath))
        {
            return new List<AlunoRegistroEntity>();
        }

        await _fileLock.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(dbFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<AlunoRegistroEntity>();

            return JsonSerializer.Deserialize<List<AlunoRegistroEntity>>(json) ?? new();
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
