# 💇‍♀️ Gerenciador de Salão de Beleza

Sistema desktop para gestão de salões de beleza, barbearias e centros estéticos, desenvolvido em **.NET 10 (WPF)** com **Entity Framework Core (SQLite)**, interface moderna (**WPF-UI**), arquitetura **MVVM** e integração com o **Google Agenda**.

O software foi concebido para funcionar de forma **100% local**, simples e robusta, operando mesmo sem conexão com a internet.

---

## 🚀 Principais Funcionalidades

### 👤 1. Módulo de Clientes
- **Cadastro Completo:** Registro de Nome, **Apelido (opcional)**, Telefone/WhatsApp, E-mail, Observações e Data de Cadastro.
- **Validação e Máscara em Tempo Real:** Validação rigorosa de nome, telefone com DDD e e-mail. Máscara automática interativa de telefone `(XX) XXXXX-XXXX` aplicada dinamicamente conforme o usuário digita, bloqueando caracteres não numéricos.
- **Busca Rápida:** Filtragem inteligente por nome, apelido, telefone (formatado ou apenas dígitos) ou e-mail.
- **Histórico Consolidado:** Consulta do histórico completo de atendimentos, agendamentos e comandas de cada cliente.
- **Soft Delete:** Exclusão lógica com data (`DeletedAt`), garantindo que o histórico contábil e as comandas passadas permaneçam íntegras.

### ✂️ 2. Módulo de Serviços e Produtos
- **Catálogo de Serviços em Cards:** Interface moderna no estilo Rosé Studio com cards visuais responsivos, ícone temático de corte/tesoura em tom blush/terracota suave, duração estimada com ícone de relógio, valor monetário em destaque e contador de serviços em tempo real.
- **Busca Rápida de Serviços:** Filtragem instantânea e reativa por nome ou descrição do serviço.
- **Modais Dedicados e Máscaras para Serviços:** Modais modernos para inclusão e edição de serviços com máscara decimal em tempo real no campo de valor (`R$`), máscara numérica no tempo estimado (min), validação defensiva em duas camadas e diálogo de confirmação seguro para exclusão.
- **Catálogo de Produtos em Cards:** Interface moderna no estilo Rosé Studio com cards visuais responsivos, ícone temático verde sálvia, valor em destaque e contadores em tempo real.
- **Busca Rápida de Produtos:** Filtragem instantânea por nome ou descrição do produto.
- **Modais Dedicados e Máscaras para Produtos:** Modais modernos para inclusão e edição de produtos com máscara decimal em tempo real no campo de valor (`R$`), além de diálogo de confirmação seguro para exclusão.
- **Soft Delete:** Desativação segura de serviços e produtos (`DeletedAt`) para não corromper comandas e agendamentos já emitidos.
- **Dados Iniciais (Seed):** Inicialização automática com serviços de estética/cabelo e cosméticos comuns para teste imediato.

### 📅 3. Módulo de Agendamentos (Agenda Inteligente)
- **Seleção Flexível:** Associação de um cliente a múltiplos serviços.
- **Tempo Previsto Automático:** Cálculo dinâmico do tempo total previsto somando a duração dos serviços selecionados.
- **Múltiplos Atendimentos Simultâneos:** Permite adicionar mais de uma agenda no mesmo horário (suporte a múltiplos profissionais ou atendimento paralelo).
- **Criação Automática de Comanda:** Ao criar um agendamento, uma comanda aberta vinculada já é gerada contendo os serviços selecionados.
- **Hard Delete:** Exclusão física definitiva de agendamentos e cancelamento automático de vínculos abertos.

### 🧾 4. Módulo de Comandas & Vendas
- **Design Moderno Inspirado no Rosé Studio:** Interface moderna em cards elegantes com bordas arredondadas, paleta suave Rosé (`#F7EBE5`), badges temáticas de status (`Aberta` em terracota suave, `Paga` em verde menta com ícone de cadeado), resumo de itens e data do atendimento.
- **Filtros por Abas Interativas e Busca Completa:** Alternância instantânea entre as abas **Todas**, **Abertas** e **Pagas**, acompanhadas de contador dinâmico, seletor de calendário (**DatePicker**) para filtrar comandas por data específica (com botão de limpeza rápida) e busca reativa por número da comanda, nome do cliente, itens inclusos ou data (ex.: `20/10/2026`).
- **Fluxo de Atendimento Ágil & Modal de Agendas do Dia:** Ao clicar em "+ Nova comanda", exibe uma listagem das agendas pendentes da data selecionada com botão direto de "Atender", além da opção funcional "+ Criar comanda do zero" para clientes sem agendamento prévio.
- **Modal Completo de Edição & Fechamento:**
  - Seleção de cliente via ComboBox com busca.
  - **Busca Rápida de Serviços e Produtos pelo Nome:** Campo de busca reativo que filtra o catálogo de serviços e produtos instantaneamente conforme a digitação.
  - **Área Rolável Dedicada para Itens da Comanda:** Contêiner rolável isolado com altura máxima definida para os itens da comanda, garantindo que adicionar múltiplos serviços e produtos não expanda o modal desproporcionalmente.
  - Edição inline de preços unitários e quantidades enquanto a comanda estiver aberta.
  - Máscara monetária em tempo real (`helpers:MaskHelper.Mask="Currency"`) que aceita digitação com ponto (`.`) ou vírgula (`,`) e formatação progressiva estilo bancário (`R$ 0,00`).
  - Duração total calculada em tempo real com formatação inteligente em horas e minutos (ex.: `2h 30min` ou `3h 00min`), eliminando exibição crua de minutos excessivos.
- **Regra de Bloqueio Estrito (Read-Only):** Comandas com status `Paga` são abertas em modo de visualização estrita, desabilitando alterações de itens, clientes e valores para proteger a integridade contábil.
- **Finalização & Conclusão Automática:** Botão "Finalizar e receber" registra a data de encerramento, fixa o valor total e atualiza o agendamento vinculado para `Concluido`.
- **Hard Delete Seguro:** Diálogo de confirmação dedicado para exclusão física definitiva de comandas canceladas/estornadas.

### 📆 5. Integração com Google Agenda (Google Calendar API v3)
- **Sincronização com o Google:** Criação automática de eventos na agenda principal do Google com título, cliente, telefone, tempo total, serviços detalhados e observações.
- **Atualização & Cancelamento:** Sincronização ao remarcar ou cancelar horários.
- **OAuth 2.0 Seguro:** Fluxo oficial para aplicativo desktop com renovação automática de tokens em segundo plano.
- **Operação Resiliente (Offline First):** Falhas de rede ou ausência de credenciais do Google não travam o sistema — os agendamentos continuam funcionando localmente.

### 📊 6. Painel de Controle (Dashboard & Gráficos)
- **Faturamento Mensal:** Gráfico dos 12 meses do ano com filtro interativo por ano.
- **Top 5 Clientes:** Ranking dos clientes que mais gastaram no salão.
- **Métricas do Período:** Total de receita e número de atendimentos concluídos.

### 🛡️ 7. Validação de Dados e Máscaras Automáticas em Tempo Real
- **Validação em Dupla Camada (Defense-in-Depth):** Todo dado é validado no ViewModel (para feedback amigável imediato) e nos Services (para garantia absoluta de integridade).
- **Máscaras Dinâmicas em Tempo Real:** Formatação instantânea de campos conforme a digitação do usuário (telefone celular `(XX) XXXXX-XXXX`, CPF `XXX.XXX.XXX-XX`, etc.).
- **Bloqueio de Caracteres Inválidos:** Comportamento anexado XAML intercepta digitações e colagens (`Ctrl+V`), impedindo a entrada de caracteres não permitidos.
- **Sanitização Automática:** Remoção de espaços espúrios, verificação de DDD e números de dígitos, e validação de e-mails de acordo com padrões modernos.

---

## 🏛️ Arquitetura e Tecnologias

O projeto segue estritamente o padrão arquitetural **MVVM (Model-View-ViewModel)** com separação clara de responsabilidades:

```text
View (XAML + WPF-UI) ──> ViewModel (CommunityToolkit.Mvvm) ──> Services ──> Data (EF Core / SQLite)
```

- **Linguagem & Framework:** C# 13+ / .NET 10 (WPF)
- **Interface Gráfica:** [WPF-UI](https://github.com/lepoco/wpfui) (estilo moderno Fluent Design)
- **MVVM Toolkit:** `CommunityToolkit.Mvvm` (geradores de código para comandos e propriedades observáveis)
- **ORM & Banco de Dados:** Entity Framework Core com provedor `Microsoft.EntityFrameworkCore.Sqlite`
- **Injeção de Dependências:** `Microsoft.Extensions.DependencyInjection`
- **Google API:** `Google.Apis.Calendar.v3` e `Google.Apis.Auth`

---

## 📁 Estrutura de Pastas

```text
gerenciador-basico-salao-de-beleza/
├── AGENTS.md                                # Diretrizes e regras arquiteturais
├── README.md                                # Documentação geral do projeto
└── GerenciadorSalao/
    ├── Data/                                # Contexto EF Core e sementes do banco
    │   ├── AppDbContext.cs
    │   └── DbInitializer.cs
    ├── Models/                              # Entidades de domínio e DTOs
    │   ├── Cliente.cs
    │   ├── Servico.cs
    │   ├── Produto.cs
    │   ├── Agendamento.cs
    │   ├── AgendamentoServico.cs
    │   ├── Comanda.cs
    │   ├── ComandaItem.cs
    │   ├── Enums (StatusAgendamento, StatusComanda, TipoItemComanda)
    │   └── DTOs/ (FaturamentoMensalDto, TopClienteDto, ComandaItemDto)
    ├── Services/                            # Regras de negócio e casos de uso
    │   ├── ClienteService.cs
    │   ├── ServicoService.cs
    │   ├── ProdutoService.cs
    │   ├── AgendamentoService.cs
    │   ├── ComandaService.cs
    │   ├── RelatorioService.cs
    │   └── GoogleCalendarService.cs
    ├── ViewModels/                          # Camada de apresentação MVVM
    ├── Views/                               # Interfaces em XAML
    ├── Helpers/                             # Utilitários e conversores
    ├── Resources/                           # Estilos, ícones e temas
    ├── App.xaml / App.xaml.cs               # Configuração do DI e startup do banco
    └── MainWindow.xaml / MainWindow.xaml.cs # Janela principal
```

---

## 💾 Persistência de Dados

- **Banco de Dados Local:** SQLite armazenado fora das pastas de código e compilação, localizado em:
  ```text
  %LOCALAPPDATA%\GerenciadorSalao\salao.db
  ```
- **Criação Automática:** O banco e as tabelas são criados automaticamente na primeira inicialização (`DbInitializer.Inicializar`).

---

## 🔑 Configuração do Google Agenda (Opcional)

Para ativar a sincronização com sua conta Google:

1. Acesse o [Google Cloud Console](https://console.cloud.google.com/).
2. Crie um projeto e ative a **Google Calendar API**.
3. Na tela de consentimento OAuth, selecione o tipo de usuário **Externo**.
4. Crie uma credencial **ID do cliente OAuth** do tipo **Aplicativo para Computador (Desktop App)**.
5. Baixe o arquivo JSON de credenciais.
6. Renomeie o arquivo para **`google_credentials.json`** e salve-o em:
   ```text
   %LOCALAPPDATA%\GerenciadorSalao\google_credentials.json
   ```
   *(ou consulte o modelo em [GerenciadorSalao/google_credentials.example.json](GerenciadorSalao/google_credentials.example.json))*.
7. No primeiro uso, o navegador será aberto para solicitar a permissão do Google Calendar.

---

## 🛠️ Como Executar o Projeto

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/) instalado.
- Sistema Operacional Windows 10/11.

### Passos
```bash
# 1. Clonar o repositório
git clone https://github.com/Necobgs/gerenciador-basico-salao-de-beleza.git

# 2. Acessar a pasta do projeto
cd gerenciador-basico-salao-de-beleza/GerenciadorSalao

# 3. Restaurar dependências e compilar
dotnet build

# 4. Executar a aplicação
dotnet run
```

---

## 🛡️ Políticas de Exclusão

| Entidade | Tipo de Exclusão | Comportamento |
| :--- | :--- | :--- |
| **Cliente** | `Soft Delete` | Preenche `DeletedAt`. Oculto em listagens normais, mas mantido para preservar histórico de comandas. |
| **Serviço** | `Soft Delete` | Preenche `DeletedAt` e inativa. Comandas antigas mantêm o histórico do serviço intacto. |
| **Produto** | `Soft Delete` | Preenche `DeletedAt` e inativa. Comandas antigas mantêm o histórico da venda. |
| **Agendamento** | `Hard Delete` | Remoção física definitiva do banco e exclusão no Google Agenda. |
| **Comanda** | `Hard Delete` | Remoção física definitiva do banco e dos itens associados. |
| **ComandaItem** | `Hard Delete` | Remoção física definitiva do item na comanda aberta. |
