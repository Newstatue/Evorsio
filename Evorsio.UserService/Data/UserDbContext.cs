using Microsoft.EntityFrameworkCore;
using Evorsio.AuthService.Models;

namespace Evorsio.AuthService.Data;


public class UserDbContext : DbContext
{
    // 构造函数，用于注入配置
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
    {
    }

    // DbSet 表示数据库表
    public DbSet<User> Users { get; set; } = null!;

    // 配置模型，可选
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User 表配置
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id); // 主键
            entity.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(u => u.Locale)
                .IsRequired()
                .HasMaxLength(10);
        });
    }
}