using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CadastroAlunos.Models;
using Microsoft.AspNetCore.Http;
using Tesseract;
using UglyToad.PdfPig;

namespace CadastroAlunos.Services;

public class DocumentVerificationService : IDocumentVerificationService
{
    private readonly ILogger<DocumentVerificationService> _logger;
    private readonly IWebHostEnvironment _env;

    private static readonly HashSet<string> Preposicoes = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "da", "do", "das", "dos", "e"
    };

    private static readonly string[] PalavrasChaveDocumento = new[]
    {
        "republica federativa", "registro geral", "carteira de identidade",
        "instituto de identificacao", "filiacao", "naturalidade",
        "data de nascimento", "secretaria de seguranca", "ssp", "detran",
        "valida em todo territorio nacional", "doc", "identidade", "cpf"
    };

    public DocumentVerificationService(ILogger<DocumentVerificationService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async Task<ResultadoValidacao> VerificarDocumentoAsync(AlunoCadastroViewModel model, IFormFile documento)
    {
        var resultado = new ResultadoValidacao
        {
            Aluno = model,
            DocumentoNomeArquivo = documento.FileName,
            DocumentoTamanhoBytes = documento.Length,
            DataProcessamento = DateTime.Now
        };

        // 1. Extração do texto do documento
        string textoExtraido = string.Empty;
        var extensao = Path.GetExtension(documento.FileName).ToLowerInvariant();

        try
        {
            using var memoryStream = new MemoryStream();
            await documento.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            if (extensao == ".pdf")
            {
                textoExtraido = ExtrairTextoPdf(fileBytes);
            }
            else if (extensao == ".png" || extensao == ".jpg" || extensao == ".jpeg")
            {
                textoExtraido = ExtrairTextoImagemOcr(fileBytes);
            }
            else
            {
                resultado.Alertas.Add($"O formato '{extensao}' não é suportado para conferência automática. Use PDF ou PNG.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar e extrair dados do documento {FileName}", documento.FileName);
            resultado.Alertas.Add("Houve uma dificuldade ao ler o arquivo do documento. O documento foi recebido, mas recomendamos verificar a nitidez.");
        }

        resultado.TextoExtraidoPreview = ObterPreviewTexto(textoExtraido);

        // 2. Análise e Confronto dos Dados
        var textoNormalizado = NormalizarTexto(textoExtraido);
        int pontuacao = 0;
        bool nomeAlunoConfirmado = false;
        bool maeConfirmada = false;
        bool paiConfirmado = false;
        bool rgNumeroConfirmado = false;
        bool formatoRgReconhecido = false;

        // A. Checagem de Termos Oficiais de RG
        foreach (var termo in PalavrasChaveDocumento)
        {
            if (textoNormalizado.Contains(NormalizarTexto(termo)))
            {
                formatoRgReconhecido = true;
                break;
            }
        }

        if (formatoRgReconhecido)
        {
            pontuacao += 10;
            resultado.DetalhesConferencia.Add("O arquivo possui características e termos oficiais de Documento de Identidade (RG).");
        }

        // B. Checagem do Nome Completo do Aluno (Peso 50)
        var similaridadeNome = CalcularSimilaridadeNome(model.NomeCompleto, textoNormalizado);
        if (similaridadeNome >= 0.70)
        {
            nomeAlunoConfirmado = true;
            int pontosNome = (int)(similaridadeNome * 50);
            pontuacao += pontosNome;
            resultado.DetalhesConferencia.Add($"Nome do Aluno confirmado no documento ({Math.Round(similaridadeNome * 100)}% de correspondência).");
        }
        else
        {
            resultado.Alertas.Add("O nome completo digitado não foi localizado de forma nítida no documento anexado.");
        }

        // C. Checagem do Nome da Mãe (Peso 25)
        if (!string.IsNullOrWhiteSpace(model.NomeDaMae))
        {
            var similaridadeMae = CalcularSimilaridadeNome(model.NomeDaMae, textoNormalizado);
            if (similaridadeMae >= 0.70)
            {
                maeConfirmada = true;
                int pontosMae = (int)(similaridadeMae * 25);
                pontuacao += pontosMae;
                resultado.DetalhesConferencia.Add($"Filiação materna (Mãe: {model.NomeDaMae}) confirmada no documento.");
            }
            else
            {
                resultado.Alertas.Add("O nome da mãe não foi claramente identificado no documento anexado.");
            }
        }

        // D. Checagem do Nome do Pai (Peso 10)
        if (model.PaiNaoDeclarado)
        {
            pontuacao += 10;
            resultado.DetalhesConferencia.Add("Conforme informado, a filiação paterna não consta ou não foi declarada.");
            paiConfirmado = true;
        }
        else if (!string.IsNullOrWhiteSpace(model.NomeDoPai))
        {
            var similaridadePai = CalcularSimilaridadeNome(model.NomeDoPai, textoNormalizado);
            if (similaridadePai >= 0.70)
            {
                paiConfirmado = true;
                int pontosPai = (int)(similaridadePai * 10);
                pontuacao += pontosPai;
                resultado.DetalhesConferencia.Add($"Filiação paterna (Pai: {model.NomeDoPai}) confirmada no documento.");
            }
            else
            {
                resultado.Alertas.Add("O nome do pai informado não foi localizado com clareza no documento.");
            }
        }

        // E. Checagem do Número do RG (Peso 5)
        if (!string.IsNullOrWhiteSpace(model.NumeroRg))
        {
            var digitosRg = Regex.Replace(model.NumeroRg, @"\D", "");
            var digitosDocumento = Regex.Replace(textoNormalizado, @"\D", "");
            if (!string.IsNullOrEmpty(digitosRg) && digitosDocumento.Contains(digitosRg))
            {
                rgNumeroConfirmado = true;
                pontuacao += 5;
                resultado.DetalhesConferencia.Add($"Número de RG {model.NumeroRg} confere perfeitamente com o documento.");
            }
        }

        resultado.PontuacaoConfianca = Math.Clamp(pontuacao, 0, 100);

        // 3. Veredito Final Adaptado para EJA
        if (nomeAlunoConfirmado && (maeConfirmada || paiConfirmado || rgNumeroConfirmado || formatoRgReconhecido || resultado.PontuacaoConfianca >= 60))
        {
            resultado.IsValido = true;
            resultado.TituloParecer = "Documento Validado com Sucesso!";
            resultado.MensagemPrincipal = $"Parabéns, {model.NomeCompleto}! Verificamos os dados do seu RG e confirmamos que o documento pertence a você. O seu cadastro e o seu documento foram registrados com segurança.";
        }
        else if (nomeAlunoConfirmado)
        {
            resultado.IsValido = true;
            resultado.TituloParecer = "Documento Aceito com Ressalvas";
            resultado.MensagemPrincipal = $"Olá, {model.NomeCompleto}! Localizamos o seu nome no documento, mas recomendamos conferir a nitidez da foto da filiação para evitar divergências futuras.";
        }
        else
        {
            resultado.IsValido = false;
            resultado.TituloParecer = "Documento Não Pôde Ser Validado";
            resultado.MensagemPrincipal = $"Atenção, {model.NomeCompleto}: Não conseguimos confirmar se o documento enviado pertence a você. Os dados lidos não conferem com o nome ou filiação digitados. Por favor, confira o arquivo ou envie uma foto mais nítida do seu RG.";
        }

        return resultado;
    }

    private string ExtrairTextoPdf(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        using var pdfStream = new MemoryStream(pdfBytes);
        using var document = PdfDocument.Open(pdfStream);

        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        return sb.ToString();
    }

    private string ExtrairTextoImagemOcr(byte[] imageBytes)
    {
        // Tenta OCR com Tesseract
        var tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!Directory.Exists(tessDataPath))
        {
            tessDataPath = Path.Combine(_env.ContentRootPath, "tessdata");
        }

        if (Directory.Exists(tessDataPath) && File.Exists(Path.Combine(tessDataPath, "por.traineddata")))
        {
            try
            {
                using var engine = new TesseractEngine(tessDataPath, "por", EngineMode.Default);
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);
                var text = page.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível executar OCR Tesseract na imagem. Usando análise heurística.");
            }
        }

        return string.Empty;
    }

    private static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var normalizedString = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var semAcentos = sb.ToString().ToLowerInvariant();
        return Regex.Replace(semAcentos, @"\s+", " ").Trim();
    }

    private static double CalcularSimilaridadeNome(string nomeProcurado, string textoDocumento)
    {
        if (string.IsNullOrWhiteSpace(nomeProcurado) || string.IsNullOrWhiteSpace(textoDocumento))
            return 0.0;

        var nomeNorm = NormalizarTexto(nomeProcurado);

        // Correspondência exata da sequência
        if (textoDocumento.Contains(nomeNorm))
            return 1.0;

        // Separar em palavras excluindo preposições (de, da, do, dos, etc)
        var partes = nomeNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                             .Where(p => !Preposicoes.Contains(p) && p.Length >= 2)
                             .ToArray();

        if (partes.Length == 0)
            return 0.0;

        int encontrados = 0;
        foreach (var parte in partes)
        {
            // Busca palavra inteira ou com pequena tolerância de OCR (Levenshtein)
            if (textoDocumento.Contains(parte))
            {
                encontrados++;
            }
            else
            {
                // Busca em tokens do documento se há palavra próxima
                var tokensDoc = textoDocumento.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bool achouFuzzy = false;
                foreach (var tok in tokensDoc)
                {
                    if (Math.Abs(tok.Length - parte.Length) <= 2)
                    {
                        var dist = CalcularLevenshtein(parte, tok);
                        if (dist <= 1 || (parte.Length >= 6 && dist <= 2))
                        {
                            achouFuzzy = true;
                            break;
                        }
                    }
                }

                if (achouFuzzy)
                {
                    encontrados++;
                }
            }
        }

        return (double)encontrados / partes.Length;
    }

    private static int CalcularLevenshtein(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        int[,] d = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = (b[j - 1] == a[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    private static string ObterPreviewTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "Nenhum texto pôde ser lido automaticamente.";

        var limpo = Regex.Replace(texto, @"\s+", " ").Trim();
        if (limpo.Length > 200)
        {
            return limpo.Substring(0, 200) + "...";
        }
        return limpo;
    }
}

