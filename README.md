# AnyComic - Sistema de Gerenciamento de Mangás

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-blue)
![Entity Framework](https://img.shields.io/badge/Entity%20Framework-9.0-green)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-blue)

Sistema web desenvolvido em ASP.NET Core MVC para gerenciamento e leitura de mangás e exibição de animes online, com área administrativa completa e sistema de autenticação.

## Diagramas

O projeto conta com diagramas técnicos que documentam a arquitetura e casos de uso do sistema:

- [Diagrama de Classes](Diagramas/DiagramaClasses.md) - Estrutura de classes e relacionamentos entre entidades
- [Diagrama de Casos de Uso](Diagramas/DiagramaCasosUso.md) - Interações entre usuários e funcionalidades do sistema

## Funcionalidades

### CRUD Completo de Mangás
- **Create** - Criar novos mangás com título, autor, descrição e capa
- **Read** - Listar e visualizar mangás cadastrados
- **Update** - Editar informações e capa dos mangás
- **Delete** - Remover mangás do sistema

### CRUD Completo de Animes
- **Create** - Criar animes (título, estúdio, descrição e capa) e adicionar episódios
- **Read** - Listar/visualizar animes e assistir aos episódios
- **Update** - Editar animes e episódios (link de vídeo, data de lançamento)
- **Delete** - Remover animes e episódios

### Recursos Adicionais
- Catálogo de mangás e animes com capas e descrições
- Sistema de leitura página por página e player de episódios (embed/stream ou arquivo)
- Cadastro e autenticação de usuários
- Sistema de favoritos para usuários (mangás e animes)
- Área administrativa protegida
- Upload de capas e páginas dos mangás
- Gestão de usuários administrativos

## Tecnologias Utilizadas

- **ASP.NET Core MVC 8.0** - Framework web principal
- **Entity Framework Core 9.0** - ORM para acesso ao banco de dados
- **PostgreSQL** - Sistema de gerenciamento de banco de dados
- **Bootstrap 5** - Framework CSS para design responsivo
- **Authentication Cookies** - Sistema de autenticação baseado em cookies

## Pré-requisitos

Antes de começar, certifique-se de ter instalado:

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) ou superior
- [PostgreSQL](https://www.postgresql.org/download/) (versão 15 ou superior)
- [pgAdmin](https://www.pgadmin.org/download/) - opcional, para gerenciar o banco
- [Visual Studio 2022](https://visualstudio.microsoft.com/) ou [VS Code](https://code.visualstudio.com/)

## Instalação e Configuração

### 1. Clone o Repositório

```bash
git clone <url-do-repositorio>
cd AnyComic-FDevs
```

### 2. Configure a Connection String

Abra o arquivo `appsettings.json` e ajuste a connection string de acordo com sua instalação do PostgreSQL:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=AnyComicDB;Username=postgres;Password=yourpassword"
  }
}
```

**Opções de Connection String:**

**Para PostgreSQL (padrão):**
```
Host=localhost;Port=5432;Database=AnyComicDB;Username=postgres;Password=yourpassword
```

**Para PostgreSQL com Usuário e Senha Customizados:**
```
Host=yourhost;Port=5432;Database=AnyComicDB;Username=your_user;Password=your_pass
```

### 3. Instale as Dependências

```bash
dotnet restore
```

### 4. Instale a Ferramenta EF Core (se ainda não tiver)

```bash
dotnet tool install --global dotnet-ef
```

Ou atualize se já tiver instalado:

```bash
dotnet tool update --global dotnet-ef
```

### 5. Execute as Migrations

**Importante:** Se você clonou o projeto que anteriormente usava SQLite, remova as migrations antigas primeiro:

```bash
dotnet ef migrations remove
```

Crie as migrations para PostgreSQL:

```bash
dotnet ef migrations add InitialCreate
```

Aplique as migrations ao banco de dados:

```bash
dotnet ef database update
```

Este comando irá:
- Criar automaticamente o banco de dados `AnyComicDB` no PostgreSQL
- Criar todas as tabelas necessárias
- Popular o banco com dados iniciais (um usuário admin padrão)

### 6. Execute a Aplicação

```bash
dotnet run
```

Ou, se estiver usando Visual Studio, pressione `F5` para executar em modo debug.

A aplicação estará disponível em:
- HTTPS: `https://localhost:7xxx`
- HTTP: `http://localhost:5xxx`

(As portas exatas serão exibidas no console ao iniciar)

## Acesso Administrativo

### Credenciais Padrão

O sistema cria automaticamente um usuário administrador:

- **Email:** `admin@anycomic.com`
- **Senha:** `admin123`

**IMPORTANTE:** Altere essas credenciais após o primeiro acesso em produção!

### Acessar Área Administrativa

1. Acesse a aplicação
2. Clique em "Login" no menu superior
3. Use as credenciais acima
4. Você será redirecionado para o painel administrativo

## Estrutura do Projeto

```
AnyComic-FDevs/
│
├── Controllers/          # Controllers MVC (lógica de negócio)
│   ├── AdminController.cs       # CRUD de mangás/animes e administração
│   ├── AccountController.cs     # Autenticação e registro
│   ├── HomeController.cs        # Páginas públicas
│   ├── MangaController.cs       # Visualização e leitura de mangás
│   └── AnimeController.cs       # Visualização e exibição de animes
│
├── Models/              # Modelos de dados (entidades)
│   ├── Manga.cs                # Entidade Mangá
│   ├── PaginaManga.cs          # Páginas dos mangás
│   ├── Anime.cs                # Entidade Anime
│   ├── Episodio.cs             # Episódios dos animes (link de vídeo)
│   ├── Usuario.cs              # Usuário comum
│   ├── UsuarioAdmin.cs         # Usuário administrador
│   ├── Favorito.cs             # Relação usuário-mangá favorito
│   └── FavoritoAnime.cs        # Relação usuário-anime favorito
│
├── Views/               # Views (interface do usuário)
│   ├── Admin/                  # Views administrativas
│   ├── Account/                # Views de autenticação
│   ├── Home/                   # Views públicas
│   ├── Manga/                  # Views de leitura
│   ├── Anime/                  # Views de exibição de animes
│   └── Shared/                 # Views compartilhadas
│
├── Data/                # Contexto do Entity Framework
│   ├── ApplicationDbContext.cs # Contexto do banco
│   └── DbInitializer.cs        # Dados iniciais
│
├── Migrations/          # Migrations do Entity Framework
├── Diagramas/          # Diagramas técnicos do projeto
│   ├── DiagramaClasses.md       # Diagrama de classes
│   └── DiagramaCasosUso.md      # Diagrama de casos de uso
│
├── wwwroot/            # Arquivos estáticos (CSS, JS, imagens)
│   ├── css/
│   ├── js/
│   ├── lib/
│   └── uploads/               # Uploads de capas e páginas
│
├── appsettings.json    # Configurações da aplicação
├── Program.cs          # Ponto de entrada da aplicação
└── README.md           # Este arquivo
```

## Estrutura do Banco de Dados

### Tabelas Principais

**Mangas**
- Id (PK)
- Titulo
- Autor
- Descricao
- ImagemCapa
- DataCriacao

**PaginasMangas**
- Id (PK)
- MangaId (FK → Mangas)
- NumeroPagina
- CaminhoImagem
- DataUpload

**Usuarios**
- Id (PK)
- Nome
- Email
- Senha (hash SHA256)
- DataCriacao

**UsuariosAdmin**
- Id (PK)
- Nome
- Email
- Senha (hash SHA256)
- IsAdmin
- DataCriacao

**Favoritos**
- Id (PK)
- UsuarioId (FK → Usuarios)
- MangaId (FK → Mangas)
- DataAdicionado

**Animes**
- Id (PK)
- Titulo
- Autor (estúdio)
- Descricao
- ImagemCapa
- DataCriacao

**Episodios**
- Id (PK)
- AnimeId (FK → Animes)
- NumeroEpisodio
- NomeEpisodio (opcional)
- LinkVideo
- DataLancamento

**FavoritosAnime**
- Id (PK)
- UsuarioId (FK → Usuarios)
- AnimeId (FK → Animes)
- DataAdicao

## Como Usar

### Criar um Novo Mangá

1. Faça login como administrador
2. No painel admin, clique em "Criar Novo Mangá"
3. Preencha os campos:
   - Título do mangá
   - Nome do autor
   - Descrição
   - Imagem de capa (opcional)
4. Clique em "Criar"

### Editar um Mangá

1. No painel admin, localize o mangá na lista
2. Clique em "Editar"
3. Modifique os campos desejados
4. Clique em "Salvar"

### Adicionar Páginas

1. No painel admin, clique em "Gerenciar Páginas" no mangá desejado
2. Selecione uma ou múltiplas imagens
3. Clique em "Upload"
4. As páginas serão numeradas automaticamente

### Excluir um Mangá

1. No painel admin, localize o mangá
2. Clique em "Excluir"
3. Confirme a exclusão
4. O mangá, suas páginas e a imagem de capa serão removidos permanentemente

## Solução de Problemas

### Erro de Conexão com PostgreSQL

**Problema:** `A network-related or instance-specific error occurred while establishing a connection to PostgreSQL`

**Solução:**
1. Verifique se o PostgreSQL está rodando:
   ```powershell
   Get-Service -Name "postgresql*"
   ```
2. Confirme a connection string no `appsettings.json`
3. Para PostgreSQL, use `localhost` e a porta `5432`

### Erro ao executar migrations

**Problema:** `The model for context has pending changes`

**Solução:**
```bash
dotnet ef migrations remove
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Metodologia e Arquitetura

Desenvolvido utilizando:
- Metodologia SCRUM de desenvolvimento ágil
- Padrão MVC (Model-View-Controller)
- Repository Pattern (via Entity Framework Core)
- Dependency Injection
- Separação de responsabilidades
- Code First com Entity Framework Core
- Autenticação baseada em Claims

---

Desenvolvido por Pedro Luiz Tunin com ASP.NET Core MVC | 2025
