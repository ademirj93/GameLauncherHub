# Guia de Distribuição, Instalação e Atualização — GameLauncher Hub

Este documento descreve as etapas para empacotamento, assinatura de código e distribuição do **GameLauncher Hub** em estações com Windows 10 e Windows 11.

---

## 1. Modos de Distribuição

### Opção A: Executável Único Independente (*Self-Contained Single-File*) — **Recomendado**
Neste modo, o runtime do .NET e todas as bibliotecas necessárias são embutidos diretamente no binário `GameLauncherHub.UI.exe`.
- **Vantagem:** O usuário final **não** precisa ter o .NET SDK ou runtime instalado previamente.
- **Tamanho:** O arquivo final possui aproximadamente 75 a 85 MB (comprimido nativamente).
- **Comando de Geração:**
  ```powershell
  .\scripts\publish.ps1 -SelfContained -Runtime win-x64
  ```

### Opção B: Dependente de Framework (*Framework-Dependent*)
Gera um executável leve que depende do runtime `Microsoft.WindowsDesktop.App` (versão 10.0 ou 8.0/9.0) instalado no Windows.
- **Vantagem:** Binário ultra-leve (< 5 MB).
- **Comando de Geração:**
  ```powershell
  .\scripts\publish.ps1 -SelfContained:$false -Runtime win-x64
  ```

---

## 2. Assinatura Digital de Código (Authenticode Code Signing)

Para evitar alertas do **Windows SmartScreen** e garantir autenticidade ao usuário final, o executável e o instalador devem ser assinados com um certificado digital comercial (EV ou Standard) utilizando a ferramenta oficial `signtool.exe` do Windows SDK:

```powershell
# Assinatura com carimbo de data/hora (Timestamp RFC 3161)
signtool.exe sign `
    /tr http://timestamp.digicert.com `
    /td sha256 `
    /fd sha256 `
    /a `
    /d "GameLauncher Hub" `
    /du "https://github.com/Antigravity/GameLauncherHub" `
    ".\publish\win-x64\GameLauncherHub.UI.exe"
```

Verificação pós-assinatura:
```powershell
signtool.exe verify /pa /v ".\publish\win-x64\GameLauncherHub.UI.exe"
```

---

## 3. Geração do Instalador Oficial com Inno Setup

O projeto inclui o script [`installer/GameLauncherHub.iss`](file:///c:/Users/Ademir/Projetos/Universal%20Game%20Hub/installer/GameLauncherHub.iss) configurado para gerar um instalador Windows profissional:

1. Instale o [Inno Setup 6](https://jrsoftware.org/isdl.php).
2. Execute o comando de compilação do instalador:
   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ".\installer\GameLauncherHub.iss"
   ```
3. O instalador final `GameLauncherHub_Setup_v1.0.0.exe` será gerado em `publish\installer\`.

### Recursos do Instalador:
- Instalação no diretório de programas do usuário (`{autopf}\GameLauncher Hub`).
- Criação opcional de atalhos no Menu Iniciar e na Área de Trabalho.
- Desinstalador limpo registrado no Painel de Controle e Configurações do Windows.
- Preservação intacta de configurações e históricos do usuário em `%LOCALAPPDATA%\GameLauncherHub`.

---

## 4. Requisitos de Sistema

- **Sistemas Suportados:**
  - Windows 11 (todas as edições, 64-bit).
  - Windows 10 (versão 1809 / Build 17763 ou superior, 64-bit).
- **Arquitetura:** `x64` (ou `ARM64` através da camada de emulação x64 nativa do Windows 11).
- **Privilégios:** Instalação e execução com privilégios normais de usuário. Elevação UAC é solicitada pontualmente apenas durante a execução de instaladores de terceiros que a exigirem.

