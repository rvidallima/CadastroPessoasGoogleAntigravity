using CadastroAlunos.Models;
using Microsoft.AspNetCore.Http;

namespace CadastroAlunos.Services;

public interface IFirebaseService
{
    Task<(string Id, string DocumentUrl)> SalvarAlunoEDocumentoAsync(
        AlunoCadastroViewModel model, 
        ResultadoValidacao validacao, 
        IFormFile documento);

    Task<AlunoRegistroEntity?> ObterRegistroPorIdAsync(string id);
    Task<List<AlunoRegistroEntity>> ListarRegistrosAsync();
}

