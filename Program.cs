using TalentDesk.Filters;
using TalentDesk.Repositories;
using TalentDesk.Services;

var builder = WebApplication.CreateBuilder(args);

// Toda tela exige login, salvo as marcadas com [Publico] (veja Filters/ControleAcessoFilter.cs)
builder.Services.AddControllersWithViews(opcoes => opcoes.Filters.Add<ControleAcessoFilter>());

// E-mails da equipe que entram como Superadmin (tela de convites).
// Configure fora do código: variável de ambiente Superadmin__Emails__0=voce@empresa.com
// (ou "dotnet user-secrets init" e depois dotnet user-secrets set "Superadmin:Emails:0" "voce@empresa.com")
AutenticacaoService.ConfigurarSuperadmins(builder.Configuration.GetSection("Superadmin:Emails").Get<string[]>());

// Sessão: guarda o usuário logado
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opcoes =>
{
    opcoes.IdleTimeout = TimeSpan.FromHours(8);
    opcoes.Cookie.HttpOnly = true;
    opcoes.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Curriculo}/{action=Index}/{id?}");

// Cria o arquivo do banco SQLite e as tabelas (se ainda não existirem)
BancoDeDados.TentarInicializar(app.Logger);

app.Run();
