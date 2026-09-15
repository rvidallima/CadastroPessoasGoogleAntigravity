using CadastroAlunos.Models;
using Microsoft.AspNetCore.Http;

namespace CadastroAlunos.Services;

public interface IDocumentVerificationService
{
    Task<ResultadoValidacao> VerificarDocumentoAsync(AlunoCadastroViewModel model, IFormFile documento);
}

