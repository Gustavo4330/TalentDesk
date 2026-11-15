using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.ViewComponents
{
    // Sino do topo.
    // Empresa: currículos novos dos últimos 7 dias.
    // Candidato: mudanças de status nas candidaturas dele nos últimos 14 dias.
    public class NotificacoesViewComponent : ViewComponent
    {
        private const int DiasAvisosCandidato = 14;

        public IViewComponentResult Invoke()
        {
            var sessao = HttpContext.Session;

            try
            {
                if (Perfis.EhCandidato(SessaoUsuario.Perfil(sessao)))
                {
                    var curriculoRepository = new CurriculoRepository();
                    int? curriculoId = curriculoRepository.ObterIdPorUsuario(SessaoUsuario.Id(sessao)!.Value);
                    var desde = DateTime.Now.AddDays(-DiasAvisosCandidato);

                    var avisos = curriculoId.HasValue
                        ? new VagaRepository().ListarHistorico(curriculoId.Value, limite: 20)
                            .Where(e => e.Status != StatusCurriculo.Novo && e.Data >= desde)
                            .Take(5)
                            .ToList()
                        : new List<EventoCandidatura>();

                    return View("Candidato", avisos);
                }

                return View(new CurriculoRepository().ListarNovos(dias: 7, limite: 5));
            }
            catch (SqliteException)
            {
                return View(new List<Curriculo>());
            }
        }
    }
}
