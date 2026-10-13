using GerenciadorSalao.Models;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Data;

public static class DbInitializer
{
    public static void Inicializar(AppDbContext context)
    {
        // Garante que o banco de dados e as tabelas existam
        context.Database.EnsureCreated();

        // Seed inicial de Serviços caso o banco esteja vazio
        if (!context.Servicos.Any())
        {
            var servicosIniciais = new List<Servico>
            {
                new() { Descricao = "Corte Feminino", Valor = 80.00m, DuracaoMinutos = 45, Ativo = true },
                new() { Descricao = "Corte Masculino", Valor = 50.00m, DuracaoMinutos = 30, Ativo = true },
                new() { Descricao = "Manicure Completa", Valor = 40.00m, DuracaoMinutos = 40, Ativo = true },
                new() { Descricao = "Pedicure", Valor = 45.00m, DuracaoMinutos = 45, Ativo = true },
                new() { Descricao = "Escova e Modelagem", Valor = 60.00m, DuracaoMinutos = 40, Ativo = true },
                new() { Descricao = "Coloração / Tintura", Valor = 150.00m, DuracaoMinutos = 90, Ativo = true },
                new() { Descricao = "Hidratação Profunda", Valor = 90.00m, DuracaoMinutos = 50, Ativo = true },
                new() { Descricao = "Design de Sobrancelhas", Valor = 35.00m, DuracaoMinutos = 25, Ativo = true }
            };

            context.Servicos.AddRange(servicosIniciais);
        }

        // Seed inicial de Produtos caso o banco esteja vazio
        if (!context.Produtos.Any())
        {
            var produtosIniciais = new List<Produto>
            {
                new() { Descricao = "Shampoo Hidratante 300ml", Valor = 55.00m, Estoque = 15, Ativo = true },
                new() { Descricao = "Condicionador Reconstrutor 300ml", Valor = 58.00m, Estoque = 12, Ativo = true },
                new() { Descricao = "Óleo Reparador de Pontas 60ml", Valor = 42.00m, Estoque = 20, Ativo = true },
                new() { Descricao = "Máscara Capilar Nutritiva 250g", Valor = 75.00m, Estoque = 8, Ativo = true }
            };

            context.Produtos.AddRange(produtosIniciais);
        }

        context.SaveChanges();
    }
}
