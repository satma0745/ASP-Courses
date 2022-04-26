# Расширение API - пользователи

В данном уроке мы расширим наш Web API, добавив пользователей.


## Содержание

1. [Регистрация пользователя](#Регистрация-пользователя)
2. [Хеширование пароля](#Хеширование-пароля)
3. [Уникальность username](#Уникальность-username)
4. [Валидация и документация](#Валидация-и-документация)
5. [Результат](#Результат)


## Регистрация пользователя

Начнём с того, что определим возможные взаимодействия с пользователем:
1. Пользователь может быть зарегистрирован
2. Пользователь может быть обновлён
3. Пользователь может авторизоваться
4. Пользователь может записаться на посещение митапа.

Авторизацию мы рассмотрим в следующий раз, так что вместе с ней откладывается апдейт и запись на митап. Остаётся только
регистрация пользователя.

Начнём с создания entity пользователя, у которой должны быть следующие поля:
1. `Id` - уникальный *неизменяемый* идентификатор, используемый программно
2. `Username` - уникальный *изменяемый* человекочитаемый идентификатор (для авторизации и упоминания пользователя в
беседах)
3. `Password` - пароль, использующийся в паре с `Username` для авторизации
4. `DisplayName` - имя пользователя (более формальное, чем `Username`).

В результате получим такую модель:
```csharp
[Table("users")]
public class UserEntity
{
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("display_name")]
    public string DisplayName { get; set; }
    
    [Column("username")]
    public string Username { get; set; }
    
    [Column("password")]
    public string Password { get; set; }
}
```

> **Note**: Не забудьте добавить `DbSet<UserEntity>` в `DatabaseContext`.

Создадим `UserController` и DTO для регистрации пользователя:
```csharp
public class UserController : ControllerBase
{
    private readonly DatabaseContext _context;

    public UserController(DatabaseContext context) =>
        _context = context;

    public async Task<IActionResult> RegisterNewUser([FromBody] RegisterUserDto registerDto)
    {
        var newUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            DisplayName = registerDto.DisplayName,
            Username = registerDto.Username,
            Password = registerDto.Password
        };
        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        var readDto = new ReadUserDto
        {
            Id = newUser.Id,
            DisplayName = newUser.DisplayName,
            Username = newUser.Username
        };
        return Ok(readDto);
    }
}

public class ReadUserDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public string Username { get; set; }
}

public class RegisterUserDto
{
    public string DisplayName { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}
```

> **Note**: При регистрации пользователя, мы хотим, чтобы пользователь указал всю информацию о себе, включая пароль,
однако, когда мы возвращаем информацию обратно, мы не должны показывать пользователю его пароль (это не безопасно и в
этом нет необходимости).

**ВНИМАНИЕ**: Пока не создавайте и не применяйте миграцию, т.к. в данный момент у нас есть проблемы с полями `Password`
и `Username`.


## Хеширование пароля

Проблема в том, что сейчас мы храним пароль в БД в открытом виде, а это значит, что все, кто получит доступ к БД
автоматически получит доступ и к паролям. Это недопустимо, т.к. доступ к БД имеют все, кто имеют доступ на production
сервер (как злоумышленники, там и штатные сотрудники поддержки).

Для хеширования пароля установим библиотеку [BCrypt.Net-Next](https://www.nuget.org/packages/BCrypt.Net-Next) и заменим
строку с присваиванием пароля на `Password = BCrypt.HashPassword(registerDto.Password)`.

Теперь можно создать и применить миграцию, проверить работоспособность action'а.


## Уникальность username

При авторизации, пользователь будет указывать `Username` и `Password`. Это значит, что хотя бы одно из этих полей (или
они в паре) должно быть уникально, иначе может возникнуть ситуация, что на одну пару `Username-Password` приходится
несколько пользователей. Мы уже определили, что `Username` – уникальный изменяемый человекочитаемый идентификатор
пользователя, так что настала пора сделать его и в правду уникальным.

Чтобы сделать это поле уникальным нужно:
1. Добавить уникальный индекс для БД (он не позволит сохранить запись, если уже существует другая запись с таким же
значением поля)
2. При регистрации пользователя проверять, не занят ли `Username`.

Для того, чтобы добавить unique constaint на поле в entity достаточно просто пометить entity атрибутом `[Index]`,
указав название поля и флаг `IsUnique`:
```csharp
[Index(nameof(Username), IsUnique = true)]
public class UserEntity
```

А чтобы проверить, не занят ли `Username`, достаточно просто добавить эту проверку в начало `RegisterNewUser` action:
```csharp
var usernameTaken = await _context.Users.AnyAsync(user => user.Username == registerDto.Username);
if (usernameTaken)
{
    return Conflict("Username already taken.");
}
```


## Валидация и документация

Осталось совсем немного – добавить валидацию и документировать action вместе с DTO:
```csharp
/// <summary>Register a new user.</summary>
/// <param name="registerDto">User registration information.</param>
/// <response code="200">Newly registered user.</response>
/// <response code="409">Failed to register a user: username already taken.</response>
[HttpPost]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<IActionResult> RegisterNewUser([FromBody] RegisterUserDto registerDto)
{
    var usernameTaken = await _context.Users.AnyAsync(user => user.Username == registerDto.Username);
    if (usernameTaken)
    {
        return Conflict("Username already taken.");
    }
    
    var newUser = new UserEntity
    {
        Id = Guid.NewGuid(),
        DisplayName = registerDto.DisplayName,
        Username = registerDto.Username,
        Password = BCrypt.HashPassword(registerDto.Password)
    };
    _context.Users.Add(newUser);
    await _context.SaveChangesAsync();

    var readDto = new ReadUserDto
    {
        Id = newUser.Id,
        DisplayName = newUser.DisplayName,
        Username = newUser.Username
    };
    return Ok(readDto);
}

public class ReadUserDto
{
    /// <summary>User identifier.</summary>
    /// <example>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</example>
    public Guid Id { get; set; }
    
    /// <summary>User display name.</summary>
    /// <example>Tony Lore</example>
    public string DisplayName { get; set; }
    
    /// <summary>Username for authorization.</summary>
    /// <example>tony_lore</example>
    public string Username { get; set; }
}

public class RegisterUserDto
{
    /// <summary>User display name.</summary>
    /// <example>Tony Lore</example>
    [Required]
    [MaxLength(50)]
    [RegularExpression(@"^[\w\s]*$")]
    public string DisplayName { get; set; }
    
    /// <summary>Username for authorization.</summary>
    /// <example>tony_lore</example>
    [Required]
    [MaxLength(30)]
    [RegularExpression(@"^[\w\s\d]*$")]
    public string Username { get; set; }
    
    /// <summary>Password for authorization.</summary>
    /// <example>password123</example>
    [Required]
    [MinLength(6)]
    [MaxLength(20)]
    [RegularExpression(@"^[\w\s\d]*$")]
    public string Password { get; set; }
}
```


## Результат

Теперь вы можете запустить приложение и проверить корректность работы action'а регистрации пользователя.

> **Note**: Не забудьте создать и применить миграции.