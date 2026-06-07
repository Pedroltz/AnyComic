using AnyComic.Models;
using System.Security.Cryptography;
using System.Text;

namespace AnyComic.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();

            // Verificar se já existe um administrador
            if (context.UsuariosAdmin.Any())
            {
                return; // BD já foi inicializado
            }

            // Criar administrador padrão
            var adminPassword = HashPassword("admin123");
            var admin = new UsuarioAdmin
            {
                Nome = "Administrador",
                Email = "admin@anycomic.com",
                Senha = adminPassword,
                IsAdmin = true,
                DataCriacao = DateTime.Now
            };

            context.UsuariosAdmin.Add(admin);
            context.SaveChanges();
        }

        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}
