# Использование EF Core Fluent API

В этом уроке мы исправим названия индексов в БД и сделаем мы это переписав конфигурацию entity'ей с аннотаций на Fluent
API.


## Содержание

1. [О Fluent API](#О-Fluent-API)
2. [Конфигурация `MeetupEntity`](#Конфигурация-MeetupEntity)
3. [Конфигурация `UserEntity` и `RefreshTokenEntity`](#Конфигурация-UserEntity-и-RefreshTokenEntity)
4. [Миграция](#Миграция)


## О Fluent API

Fluent API - способ конфигурирования entity'ей в EF Core. Мы уже применяли другой способ - аннотирование. Проблемы
аннотаций в том, что они сильно ограничены в своих возможностях: с их помощью нельзя указать названия генерируемых
индексов, создавать составные ключи и многое другое.

Конкретный синтаксис Fluent API рассмотрим во время написания конфигурации для `MeetupEntity` и `UserEntity`. Но перед
этим нужно добавить возможность его использовать. Для этого необходимо вызвать метод `ApplyConfigurationsFromAssembly`
в методе `OnModelCreating` класса `DatabaseContext`:
```csharp
public class DatabaseContext : DbContext
{
    public DbSet<MeetupEntity> Meetups { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<RefreshTokenEntity> RefreshTokens { get; set; }

    public DatabaseContext(DbContextOptions<DatabaseContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
```

## Конфигурация `MeetupEntity`

Мы будем описывать конфигурацию в том же файле, в котором объявлен сам entity. Для начала нужно удалить все уже
существующие аннотации:
```csharp
public class MeetupEntity
{
    public Guid Id { get; set; }
    public string Topic { get; set; }
    public string Place { get; set; }
    public int Duration { get; set; }
}
```

После чего можно создать класс `MeetupEntityTypeConfiguration` и в нём реализовать интерфейс
`IEntityTypeConfiguration<MeetupEntity>`. В методе `Configure` опишем конфигурацию для `MeetupEntity`:
```csharp
internal class MeetupEntityTypeConfiguration : IEntityTypeConfiguration<MeetupEntity>
{
    public void Configure(EntityTypeBuilder<MeetupEntity> entity)
    {
        entity.ToTable("meetups");

        entity
            .HasKey(meetup => meetup.Id)
            .HasName("pk_meetups");

        entity
            .Property(meetup => meetup.Id)
            .HasColumnName("id");

        entity
            .Property(meetup => meetup.Topic)
            .HasColumnName("topic");

        entity
            .Property(meetup => meetup.Place)
            .HasColumnName("place");

        entity
            .Property(meetup => meetup.Duration)
            .HasColumnName("duration");
    }
}
```

В данном случае конфигурация довольно простая, мы делаем только 3 вещи:
1. Указываем название таблицы, которая будет соответствовать entity - `meetups`
2. Указываем, какое поле является ключевым и указываем название индекса, создаваемого БД - `pk_meetups` (ранее название
было `PK_meetups` и мы не могли его изменить)
3. Указываем названия для всех полей.


## Конфигурация `UserEntity` и `RefreshTokenEntity`

С `RefreshTokenEntity` всё так же просто:
```csharp
entity.ToTable("refresh_tokens");

entity
    .HasKey(refreshToken => refreshToken.Id)
    .HasName("pk_refresh_tokens");

entity
    .Property(refreshToken => refreshToken.Id)
    .HasColumnName("id");

entity
    .Property(refreshTokenEntity => refreshTokenEntity.UserId)
    .HasColumnName("user_id");

entity
    .Property(refreshToken => refreshToken.ExpirationTime)
    .HasColumnName("expiration_time");
```

А вот с `UserEntity` несколько интереснее, т.к. нужно создать unique constraint на поле `Username`:
```csharp
entity.ToTable("users");

entity
    .HasKey(user => user.Id)
    .HasName("pk_users");

entity
    .HasIndex(user => user.Username)
    .IsUnique()
    .HasDatabaseName("ix_users_username");

entity
    .Property(user => user.Id)
    .HasColumnName("id");

entity
    .Property(user => user.DisplayName)
    .HasColumnName("display_name");

entity
    .Property(user => user.Username)
    .HasColumnName("username");

entity
    .Property(user => user.Password)
    .HasColumnName("password");
```


## Миграция

Создайте миграцию, но не применяйте её. Если мы откроем код миграции, то увидим следующиее:
```csharp
migrationBuilder.DropPrimaryKey(
    name: "PK_users",
    table: "users");

migrationBuilder.DropPrimaryKey(
    name: "PK_refresh_tokens",
    table: "refresh_tokens");

migrationBuilder.DropPrimaryKey(
    name: "PK_meetups",
    table: "meetups");

migrationBuilder.RenameIndex(
    name: "IX_users_username",
    table: "users",
    newName: "ix_users_username");

migrationBuilder.AddPrimaryKey(
    name: "pk_users",
    table: "users",
    column: "id");

migrationBuilder.AddPrimaryKey(
    name: "pk_refresh_tokens",
    table: "refresh_tokens",
    column: "id");

migrationBuilder.AddPrimaryKey(
    name: "pk_meetups",
    table: "meetups",
    column: "id");
```

Сначала сбрасываются все PK, после чего переименовывается unique constraint на поле `Username`, а потом обратно
создаются PK, но уже с новыми названиями. Иногда бывает, что EF Core криво генерирует миграцию (иногда даже с потерей
данных), так что всегда проверяйте код миграции перед тем, как её запускать. В данном случае вместо удаления и
воссоздания PK их можно просто переименовать (так же, как и unique contraint):
```csharp
migrationBuilder.RenameIndex(
    name: "PK_users",
    table: "users",
    newName: "pk_users");

migrationBuilder.RenameIndex(
    name: "IX_users_username",
    table: "users",
    newName: "ix_users_username");

migrationBuilder.RenameIndex(
    name: "PK_refresh_tokens",
    table: "refresh_tokens",
    newName: "pk_refresh_tokens");

migrationBuilder.RenameIndex(
    name: "PK_meetups",
    table: "meetups",
    newName: "pk_meetups");
```

> **Note**: Не забудьте обновить метод `Down`.

Теперь можно применить миграцию и проверить коррекность работы приложения.
