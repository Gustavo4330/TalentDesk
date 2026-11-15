using System.Net;
using Curriculo.Tests.Infra;
using TalentDesk.Models;
using TalentDesk.Services;

namespace Curriculo.Tests
{
    // Tela inicial do candidato e as regras de cálculo dela
    [Collection(ColecaoTalentDesk.Nome)]
    public class InicioCandidatoTests
    {
        private readonly Cenarios _cenarios;

        public InicioCandidatoTests(TalentDeskFactory app) => _cenarios = new Cenarios(app);

        private static string Unico(string texto) => $"{texto} {Guid.NewGuid().ToString("N")[..8]}";

        [Fact]
        public async Task Candidato_entra_direto_na_tela_inicial()
        {
            var (candidato, _, _) = await _cenarios.Candidato();

            var inicio = await candidato.Get("/Inicio");

            Assert.Equal(HttpStatusCode.OK, inicio.Status);
            Assert.Contains("Minhas candidaturas", inicio.Html);
            Assert.Contains("Seu currículo", inicio.Html);
        }

        [Fact]
        public async Task Sem_curriculo_a_tela_inicial_convida_a_cadastrar()
        {
            var (candidato, _, _) = await _cenarios.Candidato(comCurriculo: false);

            var inicio = await candidato.Get("/Inicio");

            Assert.Contains("Comece pelo seu currículo", inicio.Html);
        }

        [Fact]
        public async Task Recomenda_vagas_da_area_e_das_competencias()
        {
            var (empresa, _) = await _cenarios.Administrador(Unico("Recomenda"));
            string compativel = Unico("Analista Kotlin");
            string outra = Unico("Confeiteiro");
            await Cenarios.PublicarVaga(empresa, compativel, area: "Tecnologia", requisitos: "Kotlin\nAndroid");
            await Cenarios.PublicarVaga(empresa, outra, area: "Gastronomia", requisitos: "Confeitaria");

            var (candidato, _, _) = await _cenarios.Candidato(area: "Tecnologia", habilidades: "Kotlin, Android");
            var inicio = await candidato.Get("/Inicio");

            Assert.Contains(compativel, inicio.Html);
            Assert.DoesNotContain(outra, inicio.Html);
        }

        [Fact]
        public async Task Resumo_conta_candidaturas_em_andamento()
        {
            var (empresa, _) = await _cenarios.Administrador(Unico("Resumo"));
            int vagaId = await Cenarios.PublicarVaga(empresa, Unico("Vaga"));
            var (candidato, _, _) = await _cenarios.Candidato();
            await candidato.Post("/Vagas", $"/Vagas/Candidatar/{vagaId}");

            int candidatura = await Cenarios.CandidaturaNoQuadro(empresa, vagaId);
            await empresa.Post($"/Vagas/Detalhes/{vagaId}", $"/Vagas/AlterarStatusCandidatura/{candidatura}",
                new Dictionary<string, string> { ["status"] = "Em análise" });

            var inicio = await candidato.Get("/Inicio");
            Assert.Matches(@"Em andamento</span>\s*<span class=""card-valor"">1</span>", inicio.Html);
        }

        [Fact]
        public void Completude_aponta_o_que_falta_no_curriculo()
        {
            var curriculo = new TalentDesk.Models.Curriculo
            {
                Resumo = "Desenvolvedor com foco em back-end",
                Telefone = "11999999999",
                Habilidades = { "C#", "SQL" }
            };

            var itens = InicioCandidatoService.AvaliarCompletude(curriculo);

            Assert.Equal(7, itens.Count);
            Assert.True(itens.Single(i => i.Rotulo == "Resumo profissional").Feito);
            Assert.False(itens.Single(i => i.Rotulo == "Pelo menos 3 competências").Feito);
            Assert.False(itens.Single(i => i.Rotulo == "Perfil do LinkedIn").Feito);
        }

        [Theory]
        [InlineData("Novo", 0)]
        [InlineData("Em análise", 1)]
        [InlineData("Entrevista", 2)]
        [InlineData("Aprovado", 3)]
        [InlineData("Reprovado", 3)]
        public void Etapa_da_barra_de_progresso_segue_o_status(string status, int etapa)
        {
            Assert.Equal(etapa, InicioCandidatoViewModel.EtapaAtual(status));
        }
    }
}
