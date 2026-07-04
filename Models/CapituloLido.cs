using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models
{
    /// <summary>
    /// Registra que um usuário leu um capítulo específico de um mangá.
    /// Base do controle de progresso de leitura (marcação automática ao ler + manual).
    /// </summary>
    public class CapituloLido
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        // Coluna simples (indexada) para agrupar/contar o progresso por mangá.
        // Sem relacionamento de navegação de propósito: evita um segundo caminho de
        // cascade (Manga -> Capitulo -> CapituloLido já cobre a exclusão).
        [Required]
        public int MangaId { get; set; }

        [Required]
        public int CapituloId { get; set; }

        public DateTime DataLeitura { get; set; } = DateTime.Now;

        // Relacionamentos
        public Usuario? Usuario { get; set; }
        public Capitulo? Capitulo { get; set; }
    }
}
