using System.Net;
using Curriculo.Tests.Infra;

namespace Curriculo.Tests
{
    // Vagas por empresa, candidaturas com status próprio e vagas salvas
    [Collection(ColecaoTalentDesk.Nome)]
    public class VagasTests
    {
        private readonly Cenarios _cenarios;

        public VagasTests(TalentDeskFactory app) => _cenarios = new Cenarios(app);

        private static string Unico(string texto) => $"{texto} {Guid.NewGuid().ToString("N")[..8]}";

        [Fact]
        public async Task Administradores_da_mesma_empresa_compartilham_as_vagas()
        {
            string empresa = Unico("Acme");
            var (admin1, _) = await _cenarios.Administrador(empresa);
            var (admin2, _) = await _cenarios.Administrador(empresa);
            string titulo = Unico("Vaga compartilhada");

            int vagaId = await Cenarios.PublicarVaga(admin1, titulo);

            Assert.Contains(titulo, (await admin2.Get("/Vagas")).Html);
            Assert.Equal(HttpStatusCode.OK, (await admin2.Get($"/Vagas/Detalhes/{vagaId}")).Status);
            Assert.Equal(HttpStatusCode.OK, (await admin2.Get($"/Vagas/Editar/{vagaId}")).Status);
        }

        [Fact]
        public async Task Outra_empresa_nao_ve_nem_altera_a_vaga()
        {
            var (dono, _) = await _cenarios.Administrador(Unico("Dona"));
            var (outra, _) = await _cenarios.Administrador(Unico("Outra"));
            string titulo = Unico("Vaga privada");
            int vagaId = await Cenarios.PublicarVaga(dono, titulo);

            Assert.DoesNotContain(titulo, (await outra.Get("/Vagas")).Html);
            Assert.Equal(HttpStatusCode.Forbidden, (await outra.Get($"/Vagas/Detalhes/{vagaId}")).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await outra.Get($"/Vagas/Editar/{vagaId}")).Status);

            var encerrar = await outra.Post("/Vagas", $"/Vagas/AlterarStatus/{vagaId}",
                new Dictionary<string, string> { ["status"] = "Encerrada" });
            Assert.Equal(HttpStatusCode.Forbidden, encerrar.Status);
        }

        [Fact]
        public async Task Formulario_de_vaga_recusa_modalidade_fora_da_lista()
        {
            var (admin, _) = await _cenarios.Administrador(Unico("Validacao"));

            var resposta = await admin.Post("/Vagas/Nova", "/Vagas/Nova", new Dictionary<string, string>
            {
                ["Titulo"] = "Vaga", ["Empresa"] = "X", ["Area"] = "X", ["Cidade"] = "X",
                ["Modalidade"] = "Lunar", ["TipoContrato"] = "CLT", ["Descricao"] = "X"
            });

            Assert.Equal(HttpStatusCode.OK, resposta.Status);
            Assert.Contains("Modalidade inválida", System.Net.WebUtility.HtmlDecode(resposta.Html));
        }

        [Fact]
        public async Task Status_da_candidatura_e_separado_por_vaga_e_gera_historico()
        {
            var (empresa, _) = await _cenarios.Administrador(Unico("Processo"));
            int vagaA = await Cenarios.PublicarVaga(empresa, Unico("Vaga A"));
            int vagaB = await Cenarios.PublicarVaga(empresa, Unico("Vaga B"));
            var (candidato, curriculoId, _) = await _cenarios.Candidato();

            await candidato.Post("/Vagas", $"/Vagas/Candidatar/{vagaA}");
            await candidato.Post("/Vagas", $"/Vagas/Candidatar/{vagaB}");

            // A empresa move o candidato só na vaga A
            int candidaturaA = await Cenarios.CandidaturaNoQuadro(empresa, vagaA);
            var mover = await empresa.Post($"/Vagas/Detalhes/{vagaA}", $"/Vagas/AlterarStatusCandidatura/{candidaturaA}",
                new Dictionary<string, string> { ["status"] = "Entrevista" });
            Assert.True(mover.Redireciona($"/Vagas/Detalhes/{vagaA}"));

            var detalheA = await candidato.Get($"/Vagas/Detalhes/{vagaA}");
            var detalheB = await candidato.Get($"/Vagas/Detalhes/{vagaB}");
            Assert.Contains(">Entrevista</span>", detalheA.Html);
            Assert.Contains(">Novo</span>", detalheB.Html);

            // O status geral do currículo (painel da empresa) não muda: cada vaga tem o seu processo
            var painel = await empresa.Get($"/?visao=curriculos&selecionado={curriculoId}");
            string trechoPainel = painel.Html[painel.Html.IndexOf("id=\"painel\"", StringComparison.Ordinal)..];
            Assert.Matches(@"painel-meta"">\s*<span class=""status status-novo"">Novo</span>", trechoPainel);

            // A linha do tempo do candidato registra a mudança
            var inicio = await candidato.Get("/Inicio");
            Assert.Contains("Status alterado para Entrevista", inicio.Html);
        }

        [Fact]
        public async Task Candidato_nao_move_a_propria_candidatura()
        {
            var (empresa, _) = await _cenarios.Administrador(Unico("Mover"));
            int vagaId = await Cenarios.PublicarVaga(empresa, Unico("Vaga"));
            var (candidato, _, _) = await _cenarios.Candidato();
            await candidato.Post("/Vagas", $"/Vagas/Candidatar/{vagaId}");
            int candidatura = await Cenarios.CandidaturaNoQuadro(empresa, vagaId);

            var resposta = await candidato.Post("/Vagas", $"/Vagas/AlterarStatusCandidatura/{candidatura}",
                new Dictionary<string, string> { ["status"] = "Aprovado" });

            Assert.Equal(HttpStatusCode.Forbidden, resposta.Status);
        }

        [Fact]
        public async Task Outra_empresa_nao_move_candidato_da_vaga_alheia()
        {
            var (dona, _) = await _cenarios.Administrador(Unico("Dona"));
            var (outra, _) = await _cenarios.Administrador(Unico("Outra"));
            int vagaId = await Cenarios.PublicarVaga(dona, Unico("Vaga"));
            var (candidato, _, _) = await _cenarios.Candidato();
            await candidato.Post("/Vagas", $"/Vagas/Candidatar/{vagaId}");
            int candidatura = await Cenarios.CandidaturaNoQuadro(dona, vagaId);

            var resposta = await outra.Post("/Vagas", $"/Vagas/AlterarStatusCandidatura/{candidatura}",
                new Dictionary<string, string> { ["status"] = "Reprovado" });

            Assert.Equal(HttpStatusCode.Forbidden, resposta.Status);
        }

        [Fact]
        public async Task Vaga_encerrada_nao_aceita_candidatura()
        {
            var (empresa, _) = await _cenarios.Administrador(Unico("Encerra"));
            int vagaId = await Cenarios.PublicarVaga(empresa, Unico("Vaga"));
            await empresa.Post($"/Vagas/Detalhes/{vagaId}", $"/Vagas/AlterarStatus/{vagaId}",
                new Dictionary<string, string> { ["status"] = "Encerrada" });
            var (candidato, _, _) = await _cenarios.Candidato();

            await candidato.Post("/Vagas", $"/Vagas/Candidatar/{vagaId}");

            var quadro = await empresa.Get($"/Vagas/Detalhes/{vagaId}");
            Assert.Contains("Ninguém se candidatou", quadro.Html);
            Assert.Equal(HttpStatusCode.NotFound, (await candidato.Get($"/Vagas/Detalhes/{vagaId}")).Status);
        }

        [Fact]
        public async Task Candidatura_repetida_nao_duplica()
        {
            var (empresa, _) = await _cenarios.Administrador(Unico("Duplica"));
            int vagaId = await Cenarios.PublicarVaga(empresa, Unico("Vaga"));
            var (candidato, _, _) = await _cenarios.Candidato();

            await candidato.Post("/Vagas", $"/Vagas/Candidatar/{vagaId}");
            await candidato.Post("/Vagas", $"/Vagas/Candidatar/{vagaId}");

            var quadro = await empresa.Get($"/Vagas/Detalhes/{vagaId}");
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(quadro.Html, "id=\"mover-"));
        }

        [Fact]
        public async Task Candidato_salva_e_remove_vaga()
        {
            var (empresa, _) = await _cenarios.Administrador(Unico("Salvar"));
            string titulo = Unico("Vaga salva");
            int vagaId = await Cenarios.PublicarVaga(empresa, titulo);
            var (candidato, _, _) = await _cenarios.Candidato(comCurriculo: false);

            await candidato.Post("/Vagas", $"/Vagas/AlternarSalva/{vagaId}");
            Assert.Contains(titulo, (await candidato.Get("/Vagas?aba=salvas")).Html);

            await candidato.Post("/Vagas", $"/Vagas/AlternarSalva/{vagaId}");
            Assert.DoesNotContain(titulo, (await candidato.Get("/Vagas?aba=salvas")).Html);
        }
    }
}
