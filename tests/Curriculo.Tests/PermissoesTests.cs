using System.Net;
using Curriculo.Tests.Infra;

namespace Curriculo.Tests
{
    // Quem pode ver e fazer o quê (visitante, candidato, administrador, superadmin)
    [Collection(ColecaoTalentDesk.Nome)]
    public class PermissoesTests
    {
        private readonly TalentDeskFactory _app;
        private readonly Cenarios _cenarios;

        public PermissoesTests(TalentDeskFactory app)
        {
            _app = app;
            _cenarios = new Cenarios(app);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Painel/Relatorios")]
        [InlineData("/Vagas")]
        [InlineData("/Inicio")]
        [InlineData("/Convites")]
        [InlineData("/Curriculo/Editar/1")]
        [InlineData("/Curriculo/TestDb")]
        public async Task Visitante_e_levado_para_o_login(string caminho)
        {
            var resposta = await _app.NovoCliente().Get(caminho);

            Assert.True(resposta.Redireciona("/Usuario/Entrar"), $"{caminho}: {resposta.Status} -> {resposta.Destino}");
        }

        [Theory]
        [InlineData("/Usuario/Entrar")]
        [InlineData("/Curriculo/Login")]
        public async Task Telas_de_login_e_cadastro_sao_publicas(string caminho)
        {
            var resposta = await _app.NovoCliente().Get(caminho);

            Assert.Equal(HttpStatusCode.OK, resposta.Status);
        }

        [Fact]
        public async Task Candidato_cadastra_o_proprio_curriculo_sem_definir_o_status()
        {
            var (candidato, curriculoId, _) = await _cenarios.Candidato();

            var formulario = await candidato.Get($"/Curriculo/Editar/{curriculoId}");

            Assert.Equal(HttpStatusCode.OK, formulario.Status);
            Assert.DoesNotContain("name=\"Status\"", formulario.Html);
        }

        [Fact]
        public async Task Candidato_nao_edita_curriculo_de_outra_pessoa()
        {
            var (_, curriculoDoOutro, _) = await _cenarios.Candidato();
            var (candidato, _, _) = await _cenarios.Candidato();

            var abrir = await candidato.Get($"/Curriculo/Editar/{curriculoDoOutro}");
            var salvar = await candidato.Post("/Vagas", $"/Curriculo/Editar/{curriculoDoOutro}", new Dictionary<string, string>
            {
                ["Nome"] = "Invasor", ["Cargo"] = "X", ["Area"] = "X", ["Idade"] = "30", ["AnosExperiencia"] = "1",
                ["Email"] = "x@x.com", ["Cidade"] = "X", ["Formacao"] = "X"
            });

            Assert.Equal(HttpStatusCode.Forbidden, abrir.Status);
            Assert.Equal(HttpStatusCode.Forbidden, salvar.Status);
        }

        [Fact]
        public async Task Candidato_nao_acessa_telas_da_empresa()
        {
            var (candidato, curriculoId, _) = await _cenarios.Candidato();

            Assert.True((await candidato.Get("/")).Redireciona("/Inicio"));
            Assert.True((await candidato.Get("/Painel/Relatorios")).Redireciona("/Inicio"));
            Assert.True((await candidato.Get("/Vagas/Nova")).Redireciona("/Inicio"));

            var mudarStatus = await candidato.Post("/Vagas", $"/Curriculo/AlterarStatus/{curriculoId}",
                new Dictionary<string, string> { ["status"] = "Aprovado" });
            Assert.Equal(HttpStatusCode.Forbidden, mudarStatus.Status);
        }

        [Fact]
        public async Task Convite_cria_administrador_e_so_pode_ser_usado_uma_vez()
        {
            string email = $"cliente.{Guid.NewGuid():N}@talentdesk.test";
            string link = await _cenarios.Convite(email, "Empresa Convite");

            var cliente = _app.NovoCliente();
            var tela = await cliente.Get(link);
            Assert.Contains(email, tela.Html);
            Assert.Contains("Empresa Convite", tela.Html);

            // O e-mail enviado no formulário é ignorado: vale o do convite
            var criado = await cliente.Post(link, link, new Dictionary<string, string>
            {
                ["Email"] = "outro@hacker.test",
                ["Nome"] = "Cliente Novo",
                ["Senha"] = TalentDeskFactory.Senha,
                ["ConfirmarSenha"] = TalentDeskFactory.Senha
            });
            Assert.True(criado.Redireciona("/Usuario/Entrar"));

            var reuso = await _app.NovoCliente().Get(link);
            Assert.Contains("Convite indisponível", reuso.Html);

            var login = await _cenarios.Entrar(_app.NovoCliente(), email);
            Assert.Equal(HttpStatusCode.Redirect, login.Status);
        }

        [Fact]
        public async Task Token_de_convite_inventado_e_recusado()
        {
            var resposta = await _app.NovoCliente().Get("/Usuario/Convite?token=inventado");

            Assert.Contains("Convite indisponível", resposta.Html);
        }

        [Fact]
        public async Task Administrador_le_curriculos_e_muda_status_mas_nao_edita_o_perfil()
        {
            var (_, curriculoId, _) = await _cenarios.Candidato();
            var (admin, _) = await _cenarios.Administrador("Empresa Leitura");

            var lista = await admin.Get("/");
            Assert.Equal(HttpStatusCode.OK, lista.Status);
            Assert.DoesNotContain("/Curriculo/Editar/", lista.Html);
            Assert.DoesNotContain("Excluir currículo", lista.Html);

            Assert.Equal(HttpStatusCode.Forbidden, (await admin.Get($"/Curriculo/Editar/{curriculoId}")).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.Get("/Curriculo/Cadastrar")).Status);

            var status = await admin.Post("/", $"/Curriculo/AlterarStatus/{curriculoId}",
                new Dictionary<string, string> { ["status"] = "Entrevista", ["returnUrl"] = "/" });
            Assert.Equal(HttpStatusCode.Redirect, status.Status);
        }

        [Fact]
        public async Task Somente_o_superadmin_abre_os_convites()
        {
            var (admin, _) = await _cenarios.Administrador("Empresa Convites");
            var superadmin = await _cenarios.Superadmin();

            Assert.Equal(HttpStatusCode.Forbidden, (await admin.Get("/Convites")).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.Get("/Curriculo/TestDb")).Status);
            Assert.Equal(HttpStatusCode.OK, (await superadmin.Get("/Convites")).Status);
        }
    }
}
