<p align="center">
  <img src="logo.bmp" alt="GameLauncher Hub Logo" width="480" />
</p>

# GameLauncher Hub

<p align="center">
  <img src="https://img.shields.io/badge/Release-v0.11.16-00C853?style=for-the-badge&logo=windows" alt="Release" />
  <img src="https://img.shields.io/badge/Developer-Ademir%20Junior-6366F1?style=for-the-badge" alt="Developer" />
  <img src="https://img.shields.io/badge/License-GNU%20GPLv3-blue?style=for-the-badge" alt="License" />
  <img src="https://img.shields.io/badge/.NET-10.0%20(LTS)-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=for-the-badge&logo=windows" alt="Windows" />
</p>

Centralizador profissional de instalação, gerenciamento e execução de plataformas de jogos para Windows (PC), desenvolvido em **C#** e **.NET 10** com **WPF**, seguindo princípios rigorosos de **Clean Architecture**, **SOLID** e **Secure by Design**.

Desenvolvido por **Ademir Junior**, disponibilizado sob a licença de software livre **GNU General Public License v3.0 (GPLv3)**.

Inspirado na identidade visual do *ArtCraftLauncher*, o GameLauncher Hub unifica a detecção de ecossistemas de jogos, downloads diretos de fontes oficiais com verificação de assinatura digital **Microsoft Authenticode** e execução segura sem instâncias concorrentes.

---

## 🚀 Novidades da Release 0.11.16 (16/11/2026)

- **Correção e Estabilização dos Downloads Oficiais (GOG Galaxy, Epic Games e Battle.net):**
  - **GOG GALAXY:** Migração para o instalador oficial web permanente (`webinstallers.gog-statics.com/download/GOG_Galaxy_2.0.exe`) e homologação do domínio de CDN seguro `gog-statics.com` na whitelist de segurança.
  - **Epic Games Launcher:** Homologação dos domínios oficiais de entrega Akamai (`akamaized.net`, `epicgames-download1.akamaized.net`, `epicgamescdn.com`), liberando a resolução de redirecionamentos do pacote MSI oficial, com suporte a `/qn /norestart`.
  - **Battle.net:** Padronização do endpoint oficial canônico (`Battle.net-Setup.exe`) e inclusão dos domínios da CDN Akamai/Blizzard (`blzddist1-a.akamaihd.net`, `akamaihd.net`) na whitelist de segurança.
  - **Identificação HTTP (User-Agent):** Configuração de User-Agent de cliente Windows padrão em todas as conexões de download, prevenindo bloqueios HTTP 403 por WAFs de borda (Cloudflare e Akamai).
  - **Resiliência na Validação Authenticode & Pós-Instalação:** Compatibilidade com variações de razão social e aliases de editores de certificados (`GOG`, `Epic Games`, `Blizzard`), além de detecção assíncrona com retentativas após a conclusão do instalador.
- **Inicialização com o Sistema Minimizada no System Tray:**
  - Nova opção no menu de configurações: *"Iniciar minimizado na bandeja ao inicializar com o sistema"*, vinculada de forma reativa à inicialização do Windows.
  - O aplicativo lê parâmetros `--minimized` e carrega dados dos launchers em segundo plano sem piscar ou exibir a janela principal na tela.

---

## 🎮 Catálogo de Plataformas

O hub gerencia inicialmente **11 plataformas de jogos**:

| Plataforma | Editor | Mecanismo de Detecção Principal | Tipo de Distribuição |
|---|---|---|---|
| **Steam** | Valve Corporation | Registro (`SOFTWARE\Valve\Steam`) e App Paths | Executável Oficial (.exe) |
| **Epic Games Launcher** | Epic Games, Inc. | Registro de Desinstalação e Protocolo | Pacote MSI Oficial (.msi) |
| **Amazon Games** | Amazon Services LLC | `%LOCALAPPDATA%\Amazon Games\App` e Registro | Executável Oficial (.exe) |
| **Battle.net** | Blizzard Entertainment | Registro Blizzard e Processo Ativo | Executável Oficial (.exe) |
| **Ubisoft Connect** | Ubisoft Entertainment | Registro Ubisoft Launcher e Protocolo `uplay://` | Executável Oficial (.exe) |
| **EA app** | Electronic Arts, Inc. | Registro EA Desktop e App Paths | Executável Oficial (.exe) |
| **Heroic Games Launcher** | Heroic Open Source | Desinstalação 32/64 bits e `%LOCALAPPDATA%\Programs` | Executável Oficial (.exe) |
| **GOG GALAXY** | CD PROJEKT | Chave GalaxyClient no Registro | Executável Oficial (.exe) |
| **Playnite** | Josef Nemec | Registro Playnite e App Paths | Executável Oficial (.exe) |
| **Lutris** | Linux Gaming Platform | Determinístico: Incompatível com Windows | Apenas Linux (Educativo) |
| **Xbox App** | Microsoft Corporation | Pacotes MSIX (`AppModel\Repository\Packages`) | Pacote Windows Store / UWP |

---

## 🎨 Identidade Visual (ArtCraftLauncher Style)

- **Tema Escuro Grafite:** Paleta em carvão profundo (`#0E1015`, `#141720`) com superfícies em `#1A1D27` e bordas com relevo suave `#2C3040`.
- **Identidade Individual:** Cada card possui uma barra superior com a cor de marca da plataforma e ícone vetorial de alta fidelidade renderizado diretamente em XAML (`Geometry`/`Path`).
- **Estados Visuais Semânticos:**
  - `INSTALADO`: Badge esmeralda com botão `▶ Executar`.
  - `EM EXECUÇÃO`: Badge azul céu indicando processo ativo.
  - `DISPONÍVEL`: Badge índigo com botão `↓ Instalar`.
  - `BAIXANDO` / `INSTALANDO`: Barra de progresso animada com velocidade instantânea (KB/s ou MB/s).
  - `APENAS LINUX`: Badge ocre/âmbar com direcionamento educativo para o projeto oficial.
- **Top Bar Moderna:** Barra de título personalizada (`WindowChrome`) com pesquisa em tempo real, filtros em pílulas (*Pills*) e contadores de plataformas instaladas.

---

## 🔒 Arquitetura de Segurança (Secure by Design)

1. **Fontes Oficiais Estritas:** O sistema **nunca** utiliza links de terceiros ou mirrors não autorizados. Todos os instaladores são obtidos diretamente dos servidores e CDNs oficiais da Valve, Epic, Blizzard, EA, Ubisoft, Amazon, Microsoft, GOG, Heroic e Playnite.
2. **HTTPS Obrigatório:** URLs não criptografadas são sumariamente bloqueadas antes de qualquer requisição.
3. **Validação Authenticode (WinTrust API):** Invoca a API oficial `WinVerifyTrust` (`wintrust.dll`) do Windows para verificar se a assinatura digital do executável é válida e se a cadeia de certificação é confiável na autoridade raiz do sistema operacional.
4. **Conferência de Identidade do Editor (`ExpectedPublisher`):** Valida se o emissor do certificado digital confere com a distribuidora esperada.
5. **Inspeção de Cabeçalho PE:** Garante que o arquivo baixado possui cabeçalho executável válido (`MZ`), evitando a execução de scripts corrompidos ou páginas HTML salvas acidentalmente.
6. **Integridade SHA-256:** Gera hash criptográfico do arquivo baixado e audita no histórico local.
7. **Privilégios Mínimos:** O Hub executa sob o nível padrão de usuário (`asInvoker`). Apenas solicita elevação UAC do Windows no momento da execução do instalador.

---

## 🏛 Estrutura da Solução (Clean Architecture)

```
GameLauncherHub.sln
├── src/
│   ├── GameLauncherHub.Domain/            # Entidades, Enums de Estado e Contratos de Interfaces
│   ├── GameLauncherHub.Application/       # Casos de Uso e Orquestração Assíncrona
│   ├── GameLauncherHub.Infrastructure/    # Implementações de Registro, Authenticode, Download e Persistência
│   └── GameLauncherHub.UI/                # Interface Gráfica WPF, Design System e ViewModels (MVVM)
├── tests/
│   └── GameLauncherHub.UnitTests/        # Testes Automatizados (xUnit)
├── scripts/
│   ├── build.ps1                          # Restauração, compilação e execução de testes
│   └── publish.ps1                        # Publicação Self-Contained para Windows 10/11
└── installer/
    └── GameLauncherHub.iss                # Script de empacotamento Inno Setup
```

---

## 🚀 Como Compilar e Executar

### Pré-requisitos
- **Sistema Operacional:** Windows 10 (versão 1809 ou superior) ou Windows 11.
- **SDK:** .NET 10 SDK (ou .NET 9 / 8 Desktop SDK).

### 1. Compilação e Testes
Abra um terminal PowerShell na raiz do projeto e execute:
```powershell
.\scripts\build.ps1
```

### 2. Execução em Modo de Desenvolvimento
```powershell
dotnet run --project src\GameLauncherHub.UI\GameLauncherHub.UI.csproj
```

### 3. Publicação para Usuários Finais (Sem necessidade de .NET SDK)
Para gerar o executável único independente (*Self-Contained*):
```powershell
.\scripts\publish.ps1 -SelfContained -Runtime win-x64
```
O executável gerado em `publish\win-x64\GameLauncherHub.UI.exe` está pronto para distribuição direta.

