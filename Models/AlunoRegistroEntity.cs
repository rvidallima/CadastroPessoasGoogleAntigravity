namespace CadastroAlunos.Models;

public class AlunoRegistroEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string NomeCompleto { get; set; } = string.Empty;
    public string NomeDaMae { get; set; } = string.Empty;
    public string? NomeDoPai { get; set; }
    public bool PaiNaoDeclarado { get; set; }
    public string? NumeroRg { get; set; }

    public string Cep { get; set; } = string.Empty;
    public string Logradouro { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string? Complemento { get; set; }
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;

    public string DocumentoNomeOriginal { get; set; } = string.Empty;
    public string DocumentoNomeArmazenado { get; set; } = string.Empty;
    public long DocumentoTamanhoBytes { get; set; }
    public string DocumentoContentType { get; set; } = string.Empty;
    public string DocumentoUrl { get; set; } = string.Empty;

    public bool DocumentoValido { get; set; }
    public int PontuacaoConfianca { get; set; }
    public string ResumoValidacao { get; set; } = string.Empty;
    public List<string> DetalhesValidacao { get; set; } = new();
    public List<string> AlertasValidacao { get; set; } = new();

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

