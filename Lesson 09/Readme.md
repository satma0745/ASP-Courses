# EF Core

В этом уроке мы добавим базу данных для нашего приложения и поключим её с использованием EF Core.


## Содержание

1. [Работа с БД в .Net](#Работа-с-БД-в-Net)
2. [Предварительная настройка окружения](#Предварительная-настройка-окружения)
3. [Использование EF Core](#Использование-EF-Core)
4. [Результат](#Результат)


## Работа с БД в .Net

Для .Net есть широкий выбор различного инструментария для работы с БД. Мы рассмотрим самый популярный подод -
использование ORM, а, точнее, EF Core. **E**ntity **F**ramework **Core** это **O**bject-**R**elational **M**apper, т.е.
инструмент, инкапсулирующий работу с БД и предоставляющий нам удобные объектные интерфейсы для работы с БД.

Рассмотрим основные аспекты работы с EF Core:
1. [Context и Entity](#Context-и-Entity)
2. [IQueryable и Linq](#IQueryable-и-Linq)
3. [Миграции](#Миграции)

### Context и Entity

В EF Core, база данных представлена `Context`'ом, а таблица - `Entity`:
```csharp
class School
{
    public Guid Id { get; set; }
    public string Title { get; set; }

    public IEnumerable<Teacher> Teachers { get; set; }
}

class Teacher
{
    public Guid Id { get; set; }
    public string FullName { get; set; }

    public School School { get; set; }
}

class SchoolContext : DbContext
{
    public DbSet<School> Schools { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
}
```

В данном примере у нас есть таблица `Schools`, имеющая колонки `Id` и `Title`. Также у нас есть таблица `Teachers` с
колонками `Id` и `FullName`. Также у нас описана связь one-to-many между учителями и школами: за это отвечают поля
`School.Teachers` и `TeacherSchool` (у каждой школы есть несколько учителей - `IEnumerable<Teacher> Teachers`, а у
каждого учителя есть только одна школа - `School School`). EF Core сам настроит связи в БД, чтобы воссоздать эту схему
данных.

Также у нас объявлен класс `SchoolContext`. Он представляет нашу БД, а поля типа `DbSet<Entity>` - данные в таблицах.
Вся работа с БД будет происходить через контекст. Например, мы можем получить запись из БД, изменить её, и сохранить
изменения:
```csharp
async Task ChangeSchoolTitle(Guid schoolId, string newTitle, SchoolContext context)
{
    var school = await context.Schools.SingleAsync(school => school.Id == schoolId);
    school.Title = newTitle;
    await context.SaveChanges();
}
```

> **Note 1**: Обратите внимание, что всё взаимодействие с БД должно быть **асинхронным**: используется
`await context.Schools.SingleAsync(...)` и `await context.SaveChanges()`.

> **Note 2**: Обратите внимание, что изменения не применяются автоматически – их нужно вручную сохранить, вызвав
`.SaveChanges()`.

### IQueryable и Linq

В примере, приведённом выше, видно, что мы пользуемся не обычным `.Single`, а `.SingleAsync`. Это специальная
асинхронная версия стандартного Linq метода, предоставленная нам EF Core'ом. При работе с реляционными БД, EF Core
строит SQL-запрос на основе Linq-запроса, который мы используем в C#. Для кода, приведённого выше, запрос будет примерно
таким:
```sql
SELECT *
FROM School
WHERE School.Id = <schoolId>
LIMIT 1;
```

> **Note**: Приведённый SQL-код не будет полностью соответствовать тому, какой SQL-код сгенерирует EF Core. Данный код
был приведён просто для наглядности.

Все EF Core Linq-запросы представляют из себя `IQueryable`, в отличие от стандартного `IEnumerable`. Благодаря этому EF
Core и добивается того, чтобы запрос мог выполняться не только на объекты, которые уже загружены в память, но и
строился SQL-запрос в БД.

> **Note**: SQL-запрос строится только для SQL БД, для БД другого типа используется синтаксис запроса, поддерживаемый
БД; однако, это не меняет того факта, что мы из C# кода используем `IQueryable`. Также стоит отметить, что за генерацию
запросов к БД отвечает не сам EF Core, а специальный провайдер, который выбирается в зависимости от выбранной БД.

### Миграции

В EF Core есть 3 подхода к разработке:
1. [DB First](#DB-First)
2. [Code First](#Code-First)

> **Note**: В Entity Framework (**не Core** – это старая версия, используемая вместе с .Net Framework) был ещё один
способ: Model First. Почитать о нём можно
[здесь](#https://docs.microsoft.com/en-us/ef/ef6/modeling/designer/workflows/model-first). Данный подход не снискал
популярности, из-за чего и не был портирован на EF Core (есть и другие причины, но отсутствие популярности – основная).

#### DB First

В этом подходе мы работаем с уже созданной БД, и на её основе генерируем `Entity` и `Context`. Этот подход может быть
полезен, если:
1. Мы не контроллируем БД (схема БД может быть изменена не только нами)
2. Мы переходим с другого подхода на EF Core и хотим сохранить имеющуюся БД.

Во всех остальных случаях этот подход бесполезен. Он создаёт огромное количество проблем и неудобств, поэтому в рамках
нашего курса мы не будем его подробно рассматривать.

#### Code First

Это именно тот подход, который будем использовать мы. Он состоит из 3х этапов:
1. Описание `Entity`'ей и `Context`'а
2. Генерация и проверка миграций
3. Применение миграций.

Более подробно каждый из шагов мы рассмотрим на примере митапов уже во время перехода на EF Core.


## Предварительная настройка окружения

### Выбор СУБД

Сначала нам нужно выбрать БД, которую мы будем использовать. Самые популярные реляционные БД:
1. [MySQL](https://www.mysql.com/)
2. [PostgreSQL](https://www.postgresql.org/)
3. [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-2019)

MySQL - очень простая SQL СУБД, зачастую используемая с PHP. У неё есть некоторые особенности и проблемы (например,
разделение ядра и хранилища, не полная реализация ANSI и ISO/IEC стандартов SQL и многое другое), из-за которых я
предпочёл бы не использовать MySQL без необходимости.

PostgreSQL - бесплатная open-source СУБД с огромным количеством инструментов, прекрасно подходящая для использования в
интерпрайз разработке. Мы будем использовать именно её.

SQL Server - СУБД от Microsoft с ещё более богатым инструментарием по сравнению с PostgreSQL, однако она далеко не
бесплатна: [прайс-лист](https://www.microsoft.com/en-us/sql-server/sql-server-2019-pricing). В последнее время многие 
организации отказываются от её использования (зачастую в пользу PostgreSQL).

### Установка и настройка СУБД

Скачайте и установите PostgreSQL с официального сайта. Установка довольно простая, так что я не буду включать гайд по
ней в этот урок. Отмечу только что для работы с PostgreSQL нам понадобится только `PostgreSQL Server` и
`Command Line Tools` (остальные инструменты устанавливайте по желанию – в рамках курса я не буду их рассматривать).

После установки нам нужно будет создать пользователя. Для этого запустим `psql` утилиту и пройдём авторизацию:
1. Server - оставляем стандартный `localhost` (для этого просто нажимаем `Enter`)
2. Database - оставляем стандартный `postgres`
3. Port - указываем тот порт, который вы использовали при установке (по стандарту это `5432`)
4. Username - оставляем стандартный `postgres`
5. Password - указываем пароль, который вы указали при установке.

Чтобы создать пользователя, выполните следующий SQL-запрос:
```sql
CREATE USER db_user WITH PASSWORD 'db_user_password' CREATEDB;
```

> **Note**: Вы можете изменить имя пользователя и пароль (`db_user` и `db_user_password`) по своему желанию. Главное -
не забыть указать ваши данные в строке подключения к БД (во время перехода на EF Core).


## Использование EF Core

Для перехода на EF Core нам нужно:
1. [Установить необходимые библиотеки и инструменты](#Установка-библиотек-и-инструментов)
2. [Создать Context](#Создание-Contextа)
3. [Обновить MeetupController](#Обновление-MeetupControllerа)
4. [Сгенерировать и применить миграцию](#Генерация-и-применение-миграций)

### Установка библиотек и инструментов

Для начала, нам нужно установить 2 библиотеки:
1. `Npgsql.EntityFrameworkCore.PostgreSQL` – PostgreSQL драйвер для EF Core
2. `Microsoft.EntityFrameworkCore.Design` – Библиотека, собирающая данные для создания миграций.

Установить их можно используя NuGet Package Manager в вашей IDE, или с использованием следующих CLI команд:
```
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
```

Также нам нужно будет установить CLI-инструмент `dotnet-ef` для генерации и применения миграций:
```
dotnet tool install dotnet-ef
```
> **Note**: Данная команда выдаст ошибку. С тем, почему она выдаёт ошибку и как это исправить вам необходимо разобраться самостоятельно (своего рода домашнее задание). Вариант использовать `--global` опцию не подходит, т.к. это уже не будет локальная установка в рамках конкретного проекта.

### Создание Context'а

Для начала переименуем класс модели `Meetup` в `MeetupEntity` (находится в `Meetups/Model.cs`).

В корне проекта создадим класс `DatabaseContext` и унаследуем его от `DbContext`. В нём нам нужно объявить property
`Meetups` типа `DbSet<MeetupEntity>`. Также нам нужено переопределить метод `OnConfiguring`, вызвав `options.UseNpgsql`
и передав туда [строку подключения к PostgreSQL БД](https://www.connectionstrings.com/npgsql/standard).

Должен получиться следующий код:
```csharp
namespace Meets.WebApi;

using Microsoft.EntityFrameworkCore;
using Meets.WebApi.Meetup;

internal class DatabaseContext : DbContext
{
    public DbSet<MeetupEntity> Meetups { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseNpgsql("Server=localhost;Port=5432;Database=asp_courses;User Id=db_user;Password=db_user_password");
}
```

### Обновление MeetupController'а

Теперь нам нужно обновить наш `MeetupController`. Для начала, заменим список митапов на только что созданный
`DatabaseContext`:
```csharp
private readonly DatabaseContext _context = new();
```

Далее нужно перевести все action'ы с использования списка на использование EF Core. Рассмотрим перевод action'а на
получение всех митапов. Раньше он выглядел так:
```csharp
public IActionResult GetAllMeetups()
{
    var readDtos = Meetups.Select(meetup => new ReadMeetupDto
    {
        Id = meetup.Id,
        Topic = meetup.Topic,
        Place = meetup.Place,
        Duration = meetup.Duration
    });
    
    return Ok(readDtos);
}
```

Его EF Core версия выглядит так:
```csharp
public async Task<IActionResult> GetAllMeetups()
{
    var meetups = await _context.Meetups.ToListAsync();

    var readDtos = meetups.Select(meetup => new ReadMeetupDto
    {
        Id = meetup.Id,
        Topic = meetup.Topic,
        Place = meetup.Place,
        Duration = meetup.Duration
    });
    return Ok(readDtos);
}
```
Рассмотрим отличия:
1. Возвращаемый тип изменился с `IActionResult` на `Task<IActionResult>`: это было сделано для того, чтобы мы погли
асинхронно взаимодействовать с БД
2. Для получения митапов из БД теперь необходимо загрузить их в список с помощью вызова `.ToListAsync`

Остальные action'ы можете перевести самостоятельно по аналогии с action'ом `GetAllMeetups` и с использованием информации
из [секции про работу с БД в .Net](#Работа-с-БД-в-Net). В результате остальные action'ы должены выглядеть следующим
образом:
```csharp
public async Task<IActionResult> CreateMeetup([FromBody] CreateMeetupDto createDto)
{
    var newMeetup = new MeetupEntity
    {
        Id = Guid.NewGuid(),
        Topic = createDto.Topic,
        Place = createDto.Place,
        Duration = createDto.Duration
    };
    
    _context.Meetups.Add(newMeetup);
    await _context.SaveChangesAsync();

    var readDto = new ReadMeetupDto
    {
        Id = newMeetup.Id,
        Topic = newMeetup.Topic,
        Place = newMeetup.Place,
        Duration = newMeetup.Duration
    };
    return Ok(readDto);
}

public async Task<IActionResult> UpdateMeetup([FromRoute] Guid id, [FromBody] UpdateMeetupDto updateDto)
{
    var oldMeetup = await _context.Meetups.SingleOrDefaultAsync(meetup => meetup.Id == id);
    if (oldMeetup is null)
    {
        return NotFound();
    }

    oldMeetup.Topic = updateDto.Topic;
    oldMeetup.Place = updateDto.Place;
    oldMeetup.Duration = updateDto.Duration;
    await _context.SaveChangesAsync();
    
    return NoContent();
}

public async Task<IActionResult> DeleteMeetup([FromRoute] Guid id)
{
    var meetupToDelete = await _context.Meetups.SingleOrDefaultAsync(meetup => meetup.Id == id);
    if (meetupToDelete is null)
    {
        return NotFound();
    }
    
    _context.Meetups.Remove(meetupToDelete);
    await _context.SaveChangesAsync();

    var readDto = new ReadMeetupDto
    {
        Id = meetupToDelete.Id,
        Topic = meetupToDelete.Topic,
        Place = meetupToDelete.Place,
        Duration = meetupToDelete.Duration
    };
    return Ok(readDto);
}
```

### Генерация и применение миграций

Нам осталось только сгенерировать миграции БД. Для того, чтобы сгенерировать миграцию, выполните следующую команду:
```
dotnet ef migrations add "AddMeetups"
```
`AddMeetups` - название миграции. Вы можете его изменить по своему желанию, но лучше использовать понятные и
описательные названия: в данном случае мы добавляем митапы в ДБ.

После выполнения данной команды, в корне проекта была создана папка `Migrations`, а в ней 3 файла:
1. `xxxxxxxxxxxxxx_AddMeetups.cs` - основной файл с кодом миграции (вместо `xxxxxxxxxxxxxx` у вас будет дата создания
миграции в формате `yyyyMMddHHmms` по часовому поясу GMT+0)
2. `xxxxxxxxxxxxxx_AddMeetups.Designer.cs` - ожидаемая EF Core'ом схема БД после применения миграции
3. `DatabaseContextModelSnapshot` - ожидаемая EF Core'ом схема БД после применения всех миграций.

Теперь мы можем применить сгенерированную миграцию к БД:
```
dotnet ef database update
```
В процессе применения первой миграции, `dotnet-ef` так же создаст БД, если она ещё не создана.

После выполнения этой команды мы можем посмотреть, как выглядит сгенерированная БД. Для этого запустим `psql` утилиту
и пройдём авторизацию:
1. Server - оставляем стандартный `localhost`
2. Database - указываем имя БД из строки подключения (в моём случае `asp_courses`)
3. Port - указываем тот порт, который вы использовали при установке (по стандарту это `5432`)
4. Username - указываем имя пользователя из строки подключения (в моём случае `db_user`)
5. Password - указываем пароль, который вы указали при установке.

Выполним команду `\dt` для отображения всех существующих таблиц:
| Schema | Name                  | Type  | Owner   |
| :----- | :-------------------- | :---- | :------ |
| public | Meetups               | table | db_user |
| public | __EFMigrationsHistory | table | db_user |

Таблица `Meetups` содержит все митапы. На данный момент она пуста. Таблица `__EFMigrationsHistory` содержит список всех
применённых в БД миграций.

Выполним запрос на получение списка применённых миграций:
```sql
SELECT * FROM "__EFMigrationsHistory";
```
Результат:
| MigrationId               | ProductVersion |
| :------------------------ | :------------- |
| xxxxxxxxxxxxxx_AddMeetups | 6.0.0          |

Как мы видим, наша миграция была применена успешно.


## Результат

Запустим приложение и убедимся, что всё работает как и раньше. Единственное отличие – теперь при перезапуске приложения
мы не теряем все данные, т.к. они сохраняются в БД.
