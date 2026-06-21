using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using AnyComic.Data;
using AnyComic.Services;

// Cria o builder da aplicação web
var builder = WebApplication.CreateBuilder(args);

// ===== CONFIGURAÇÃO DE SERVIÇOS (Dependency Injection) =====

// Adiciona suporte para Controllers com Views (padrão MVC)
builder.Services.AddControllersWithViews();

// Serviços de domínio
builder.Services.AddScoped<IAnimeService, AnimeService>();

// ===== LIMITES DE UPLOAD (vídeos de episódios podem ser grandes) =====
// O Kestrel limita o corpo da requisição a ~28 MB por padrão. Sem isso,
// o upload de vídeos maiores é interrompido e o navegador recebe uma
// resposta vazia (NS_ERROR_NET_EMPTY_RESPONSE).
const long maxUploadBytes = 2L * 1024 * 1024 * 1024; // 2 GB

builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadBytes;
});

// Limite do parser de formulários multipart (upload de arquivos)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

<<<<<<< Updated upstream
// Configurar Entity Framework Core com SQL Server
// A connection string é lida do arquivo appsettings.json
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
=======
// Configurar Entity Framework Core com Npgsql
// A connection string é lida do arquivo appsettings.json
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
>>>>>>> Stashed changes

// Configurar autenticação baseada em cookies
// Sistema de login que mantém o usuário autenticado entre requisições
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";              // Rota para página de login
        options.LogoutPath = "/Account/Logout";            // Rota para logout
        options.AccessDeniedPath = "/Account/AccessDenied"; // Rota para acesso negado
        options.ExpireTimeSpan = TimeSpan.FromHours(24);   // Cookie expira após 24 horas
        options.SlidingExpiration = true;                  // Renova o cookie a cada requisição
    });

// Configurar sessões para manter estado do usuário
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);  // Sessão expira após 24 horas de inatividade
    options.Cookie.HttpOnly = true;                // Cookie acessível apenas via HTTP (segurança)
    options.Cookie.IsEssential = true;             // Cookie essencial para o funcionamento
});

// Constrói a aplicação
var app = builder.Build();

// ===== INICIALIZAÇÃO DO BANCO DE DADOS =====

// Cria um escopo temporário para acessar serviços e inicializar o banco
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Obtém o contexto do banco de dados
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Inicializa o banco com dados padrão (usuário admin, etc.)
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        // Em caso de erro, registra no log
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocorreu um erro ao inicializar o banco de dados.");
    }
}

// ===== CONFIGURAÇÃO DO PIPELINE DE REQUISIÇÕES HTTP =====

// Em produção, usa página de erro customizada
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");  // Redireciona erros para página de erro
    app.UseHsts();                           // Adiciona cabeçalho HSTS para segurança
}

// Redireciona requisições HTTP para HTTPS
app.UseHttpsRedirection();

// Habilita arquivos estáticos (CSS, JS, imagens) da pasta wwwroot
app.UseStaticFiles();

// Habilita roteamento de URLs para controllers/actions
app.UseRouting();

// IMPORTANTE: A ordem dos middlewares é crítica!
app.UseSession();        // 1º - Gerenciamento de sessão
app.UseAuthentication(); // 2º - Identifica o usuário (quem é?)
app.UseAuthorization();  // 3º - Verifica permissões (pode acessar?)

// Define a rota padrão do MVC
// Padrão: /Controller/Action/Id
// Exemplo: /Home/Index ou /Admin/CreateManga/5
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Inicia a aplicação
app.Run();
