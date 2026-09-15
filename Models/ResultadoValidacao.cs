namespace CadastroAlunos.Models;

public class ResultadoValidacao
{
    public bool IsValido { get; set; }
    public int PontuacaoConfianca { get; set; } // 0 a 100%
    public string TituloParecer { get; set; } = string.Empty;
    public string MensagemPrincipal { get; set; } = string.Empty;
    public List<string> DetalhesConferencia { get; set; } = new();
    public List<string> Alertas { get; set; } = new();

    public string? TextoExtraidoPreview { get; set; }
    public string DocumentoNomeArquivo { get; set; } = string.Empty;
    public long DocumentoTamanhoBytes { get; set; }
    public string DocumentoTamanhoFormatado => $"{(DocumentoTamanhoBytes / (1024.0 * 1024.0)):N2} MB";
    public string? DocumentoUrlOuCaminho { get; set; }
    public string? FirebaseRegistroId { get; set; }
    public DateTime DataProcessamento { get; set; } = DateTime.Now;

    public AlunoCadastroViewModel Aluno { get; set; } = new();
}

