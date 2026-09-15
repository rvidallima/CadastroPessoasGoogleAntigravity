using System.Text;
using CadastroAlunos.Models;
using CadastroAlunos.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace CadastroAlunos.Tests;

public class VerificacaoDocumentoTests
{
    private readonly DocumentVerificationService _service;

    public VerificacaoDocumentoTests()
    {
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

        _service = new DocumentVerificationService(
            NullLogger<DocumentVerificationService>.Instance, 
            envMock.Object);
    }

    [Fact]
    public async Task DocumentoPdf_ComNomeEFiliacaoCorrespondentes_DeveAprovarValidacao()
    {
        // 1. Gera PDF de teste com dados de RG oficial
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        
        page.AddText("REPUBLICA FEDERATIVA DO BRASIL", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);
        page.AddText("REGISTRO GERAL - CARTEIRA DE IDENTIDADE", 10, new UglyToad.PdfPig.Core.PdfPoint(50, 730), font);
        page.AddText("NOME: JOAO CARLOS DA SILVA", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
        page.AddText("FILIACAO: MARIA APARECIDA DA SILVA", 10, new UglyToad.PdfPig.Core.PdfPoint(50, 680), font);
        page.AddText("PAI: JOSE ROBERTO DA SILVA", 10, new UglyToad.PdfPig.Core.PdfPoint(50, 660), font);
        page.AddText("RG: 45.123.789-0", 10, new UglyToad.PdfPig.Core.PdfPoint(50, 640), font);

        var pdfBytes = builder.Build();

        var formFile = new FormFile(
            new MemoryStream(pdfBytes), 
            0, 
            pdfBytes.Length, 
            "Documento", 
            "rg_joao_carlos.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var aluno = new AlunoCadastroViewModel
        {
            NomeCompleto = "João Carlos da Silva",
            NomeDaMae = "Maria Aparecida da Silva",
            NomeDoPai = "José Roberto da Silva",
            NumeroRg = "45.123.789-0",
            Cep = "01001-000",
            Logradouro = "Praça da Sé",
            Numero = "100",
            Bairro = "Sé",
            Cidade = "São Paulo",
            Estado = "SP"
        };

        // Act
        var resultado = await _service.VerificarDocumentoAsync(aluno, formFile);

        // Assert
        Assert.True(resultado.IsValido);
        Assert.True(resultado.PontuacaoConfianca >= 70);
        Assert.Contains("validado com sucesso", resultado.TituloParecer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(resultado.DetalhesConferencia, d => d.Contains("Nome do Aluno confirmado"));
    }

    [Fact]
    public async Task DocumentoPdf_ComNomeDivergente_DeveRejeitarValidacao()
    {
        // 1. Gera PDF de teste com dados de outra pessoa
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        
        page.AddText("REPUBLICA FEDERATIVA DO BRASIL", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);
        page.AddText("NOME: RICARDO MENEZES SANTOS", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
        page.AddText("FILIACAO: TEREZA MENEZES", 10, new UglyToad.PdfPig.Core.PdfPoint(50, 680), font);

        var pdfBytes = builder.Build();

        var formFile = new FormFile(
            new MemoryStream(pdfBytes), 
            0, 
            pdfBytes.Length, 
            "Documento", 
            "rg_outro.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var aluno = new AlunoCadastroViewModel
        {
            NomeCompleto = "Ana Paula dos Santos",
            NomeDaMae = "Francisca dos Santos",
            PaiNaoDeclarado = true,
            Cep = "01001-000",
            Logradouro = "Rua Teste",
            Numero = "12",
            Bairro = "Bairro",
            Cidade = "Campinas",
            Estado = "SP"
        };

        // Act
        var resultado = await _service.VerificarDocumentoAsync(aluno, formFile);

        // Assert
        Assert.False(resultado.IsValido);
        Assert.True(resultado.Alertas.Count > 0);
        Assert.Contains("Não Pôde Ser Validado", resultado.TituloParecer);
    }

    [Fact]
    public void RestricaoTamanhoArquivo_CincoMegabytes_CalculoCorreto()
    {
        const long maxPermitido = 5 * 1024 * 1024; // 5MB = 5.242.880 bytes
        Assert.Equal(5242880, maxPermitido);

        long arquivo4Mb = 4 * 1024 * 1024;
        long arquivo6Mb = 6 * 1024 * 1024;

        Assert.True(arquivo4Mb <= maxPermitido);
        Assert.False(arquivo6Mb <= maxPermitido);
    }
}
