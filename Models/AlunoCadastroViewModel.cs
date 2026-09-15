using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CadastroAlunos.Models;

public class AlunoCadastroViewModel
{
    [Required(ErrorMessage = "Por favor, informe o seu Nome Completo.")]
    [Display(Name = "Nome Completo do Aluno")]
    [StringLength(150, MinimumLength = 5, ErrorMessage = "O nome completo deve ter pelo menos 5 caracteres.")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Por favor, informe o Nome da Mãe.")]
    [Display(Name = "Nome Completo da Mãe")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "O nome da mãe deve ter pelo menos 3 caracteres.")]
    public string NomeDaMae { get; set; } = string.Empty;

    [Display(Name = "Pai não declarado na certidão / RG")]
    public bool PaiNaoDeclarado { get; set; } = false;

    [Display(Name = "Nome Completo do Pai")]
    [StringLength(150, ErrorMessage = "O nome do pai deve ter no máximo 150 caracteres.")]
    public string? NomeDoPai { get; set; }

    [Display(Name = "Número do RG (Opcional)")]
    [StringLength(25, ErrorMessage = "O número do RG deve ter no máximo 25 caracteres.")]
    public string? NumeroRg { get; set; }

    // Endereço Completo
    [Required(ErrorMessage = "Por favor, informe o CEP.")]
    [Display(Name = "CEP")]
    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "Digite um CEP válido com 8 números (ex: 01001-000).")]
    public string Cep { get; set; } = string.Empty;

    [Required(ErrorMessage = "Por favor, informe a Rua ou Avenida.")]
    [Display(Name = "Logradouro (Rua, Avenida, Travessa)")]
    public string Logradouro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Por favor, informe o Número.")]
    [Display(Name = "Número")]
    public string Numero { get; set; } = string.Empty;

    [Display(Name = "Complemento (Apto, Casa 2, Bloco - Opcional)")]
    public string? Complemento { get; set; }

    [Required(ErrorMessage = "Por favor, informe o Bairro.")]
    [Display(Name = "Bairro")]
    public string Bairro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Por favor, informe a Cidade.")]
    [Display(Name = "Cidade")]
    public string Cidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Por favor, informe o Estado.")]
    [Display(Name = "Estado (UF)")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Informe a sigla do estado com 2 letras (ex: SP, RJ).")]
    public string Estado { get; set; } = string.Empty;

    // Documento Comprobatório (RG)
    [Required(ErrorMessage = "Por favor, selecione o arquivo do seu documento (RG).")]
    [Display(Name = "Documento Comprobatório (RG)")]
    public IFormFile? Documento { get; set; }
}

