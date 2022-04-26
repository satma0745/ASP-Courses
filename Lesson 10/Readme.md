# Настройка EF Core

В прошлом уроке мы подключили PostgreSQL к нашему приложению используя EF Core. Для упрощения я опустил некоторые
подробности по настройке EF Core, так что сейчас настало время исправлять допущенные ранее ошибки.


## Содержание

1. [Хранение DB Connection String](#Хранение-DB-Connection-String)
2. [Названия и полей и таблиц в PostgreSQL](#Названия-и-полей-и-таблиц-в-PostgreSQL)


## Хранение DB Connection String

### Где нужно хранить настройки приложения

В предыдущем уроке мы записали строку подключения к БД в методе `OnConfiguring` нашего `DatabaseContext`:
```csharp
protected override void OnConfiguring(DbContextOptionsBuilder options) =>
    options.UseNpgsql("Server=localhost;Port=5432;Database=asp_courses;User Id=db_user;Password=db_user_password");
```

Это неправильный подход по нескольким причинам:
1. Мы не сможем изменить параметры работы приложения без изменения его кода
2. Строка подключения это секретные данные, т.к. имея её, можно получить доступ ко всем данным приложения
3. Разработчикам нужно избегать коммита своей локальной строки подключения в репозиторий (чтобы production-версия
приложения не попыталась воспользоваться локальной базой данных).

Решить эту проблему можно вынеся строку подключения куда-нибудь во внешнюю среду. Ранее (при создании проекта) мы
рассматривали некоторые способы хранения настроек приложения:
- Переменные окружения
- [Secrets Manager](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- `appsettings.json` файлы.

Мы воспользуемся последним вариантом, т.к. он кажется мне самым простым и удобным. Вы также можете ознакомиться и с
другими вариантами и выбрать тот, который вам нравится больше всего.

### Перенос DB Connection String в appsettings

В корне проекта нужно создать файл `appsettings.Development.json` со следующим содержимым:
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Server=localhost;Port=5432;Database=asp_courses;User Id=db_user;Password=db_user_password"
  }
}
```

> **Note**: Как уже было сказано ранее, строка подключения – секретная информация, и она не должна быть закомичена в
систему контроля версий. Если вы используете `git`, то добавьте файл `appsettings.Development.json` в `.gitignore`.

У ASP приложения есть 3 стандартных состояния:
1. `Development` - приложение запущено локально для разработки
2. `Staging` - приложение запущено для тестирования
3. `Production` - приложение задеплоено на сервер и работает в штатном режиме.

Указывая `Development` в названии файла настроек, мы подчёркиваем то, что этот файл с настройками будет использоваться
локально и только во время разработки.

Теперь нам нужно подключить файл с настройками приложения и получить из него `PostgreSQL Connection String`. Для этого в
модуле `Program`, сразу после создания `builder`'а приложения, нужно собрать конфигурацию приложения, добавив в неё файл
`appsettings.Development.json`:
```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json")
    .Build();
```
Обратите внимание, что мы выбираем название файла в зависимости от того, в каком стостоянии находится наше приложение
(`Development`, `Staging` или `Production`).

Получить Connection String из файла и применить его для `DatabaseContext` можно следующим образом:
```csharp
builder.Services.AddDbContext<DatabaseContext>(options =>
{
    var connectionString = configuration.GetConnectionString("PostgreSQL");
    options.UseNpgsql(connectionString);
});
```

Метод `.GetConnectionString` получает значение поля (название которого передаётся ему в качестве параметра) из секции
`"ConnectionStrings"`. Далее мы просто вызываем тот же метод, который мы вызывали раньше внутри метода `OnConfiguring` и
передаём в него полученную строку подключения.

В результате модуль `Program` выглядит так:
```csharp
using System.Reflection;
using Meets.WebApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configuration = new ConfigurationBuilder()
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json")
    .Build();

builder.Services.AddSwaggerGen(options =>
{
    var projectDirectory = AppContext.BaseDirectory;
    
    var projectName = Assembly.GetExecutingAssembly().GetName().Name;
    var xmlFileName = $"{projectName}.xml";
    
    options.IncludeXmlComments(Path.Combine(projectDirectory, xmlFileName));
});
builder.Services.AddDbContext<DatabaseContext>(options =>
{
    var connectionString = configuration.GetConnectionString("PostgreSQL");
    options.UseNpgsql(connectionString);
});
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

app.Run();
```

Чтобы метод `.AddDbContext` смог передать настройки в наш `DatabaseContext`, нужно добавить в контекст конструктор,
который будет принимать `DbContextOptions<DatabaseContext>` и передавать их в базовый класс. Также, можем убрать
переопределение метода `OnConfiguring` за ненадобность. В результате `DatabaseContext` выглядит следующим образом:
```csharp
class DatabaseContext : DbContext
{
    public DbSet<MeetupEntity> Meetups { get; set; }

    public DatabaseContext(DbContextOptions<DatabaseContext> options)
        : base(options)
    {
    }
}
```

Осталось лишь обновить `MeetupController`. Благодаря вызову `.AddDbContext` внутри модуля `Program`, контекст будет
автоматически передаваться в конструктор нашему контроллеру (это называется
[Dependency Injection](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection) и будет
рассмотрено далее в рамках другого урока). Так что нам нужно несколько изменить `MeetupController`:
```csharp
public class MeetupController : ControllerBase
{
    private readonly DatabaseContext _context;

    public MeetupController(DatabaseContext context) =>
        _context = context;
    
    /* Action'ы опущены для краткости. Удалять их не нужно */
}
```

> **Note**: Нам нужно изменить модификаторы доступа у `DatabaseContext` на `public` (т.к. мы получаем контекст в
публичном конструкторе публичного класса `MeetupController`). Также нужно поступить и с `MeetupEntity` (т.к. публичное
поле `Meetups` публичного класса `DatabaseContext` имеет этот тип).

### Результат переноса DB Connection String

Запустите приложение и убедитесь, что всё работает и ничего не сломалось.


## Названия и полей и таблиц в PostgreSQL

Если мы откроем файл миграции, то увидим следующий код:
```csharp
migrationBuilder.CreateTable(
    name: "Meetups",
    columns: table => new
    {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        Topic = table.Column<string>(type: "text", nullable: false),
        Place = table.Column<string>(type: "text", nullable: false),
        Duration = table.Column<int>(type: "integer", nullable: false)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_Meetups", x => x.Id);
    });
```

Как мы видим, название таблицы указано с большой буквы (то же касается и названий стобцов). То же самое можно увидеть,
выполнив команду `\d "Meetups"` в `psql`:
| Column   | Type    | Collation | Nullable | Default |
| :------- | :------ | --------- | :------- | ------- |
| Id       | uuid    |           | not null |         |
| Topic    | text    |           | not null |         |
| Place    | text    |           | not null |         |
| Duration | integer |           | not null |         |

При работе с `PostgreSQL` принято использовать `snake_case` для всех имён: это просто удобнее, т.к. не нужно писать
`"Meetup"` и достаточно написать просто `meetup`.

Чтобы изменить название таблицы, добавим атрибут `[Table(...)]` для класса `MeetupEntity`, а для изменения названий
полей – воспользуемся атрибутом `[Column(...)]`:
```csharp
[Table("meetups")]
public class MeetupEntity
{
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("topic")]
    public string Topic { get; set; }
    
    [Column("place")]
    public string Place { get; set; }
    
    [Column("duration")]
    public int Duration { get; set; }
}
```

Сгенерируем новую миграцию `FixMeetupNaming`:
```csharp
migrationBuilder.DropPrimaryKey(
    name: "PK_Meetups",
    table: "Meetups");

migrationBuilder.RenameTable(
    name: "Meetups",
    newName: "meetups");

migrationBuilder.RenameColumn(
    name: "Topic",
    table: "meetups",
    newName: "topic");

migrationBuilder.RenameColumn(
    name: "Place",
    table: "meetups",
    newName: "place");

migrationBuilder.RenameColumn(
    name: "Duration",
    table: "meetups",
    newName: "duration");

migrationBuilder.RenameColumn(
    name: "Id",
    table: "meetups",
    newName: "id");

migrationBuilder.AddPrimaryKey(
    name: "PK_meetups",
    table: "meetups",
    column: "id");
```
Как мы видим, название таблицы и названия всех полей теперь в `snake_case`.

> **Note**: У нас всё ещё осталось название, написанное не в `snake_case` – `"PK_meetups"`. Мы исправим его в другом
уроке, т.к. сделать это сложнее, чем изменить названия поля или таблицы.

Примените миграцию и запустите приложение, чтобы убедиться, что всё работает как раньше и ничего не сломалось.
