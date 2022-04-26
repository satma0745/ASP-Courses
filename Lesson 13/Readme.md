# Рефакторинг: создание JWT Token Helper'а

В данный момент весь неконфигурационный код нашего приложения находится в контроллерах: `UserController` и
`MeetupController`. Также можно обратить внимание на то, что у нас есть 2 довольно больших action'а: `RefreshTokenPair`
на 101 строку и `AuthenticateUser` на 72 строки. Из-за того, что весь код собран в кучу (при том в довольно большие
целостные блоки), становится значительно сложнее его поддерживать и расширять.

В этом и последующих уроках мы будем решать эту проблему. Начнём с того, что вынесем всю логику создания и проверки JWT
токенов в отдельный сервис. Создадим папку `Helpers` в корне проекта, а в ней класс `JwtTokenHelper`.


## Содержание

1. [Парсинг и валидация токена](#Парсинг-и-валидация-токена)
2. [Выпуск-токенов](#Выпуск-токенов)
3. [Рефакторинг-сервиса](#Рефакторинг-сервиса)
4. [Рефакторинг `UserController`'а](#Рефакторинг-UserControllerа)

## Парсинг и валидация токена

Если внимательно изучить код `RefreshTokenPair` action'а, то можно заметить, что парсинг и валидация `RT` доставляет
слишком много проблем:
1. Довольно много места занимает описание параметров валидации
2. Метод `ValidateToken` принимает 3й параметр, который мы не используем (к тому же это не просто параметр, а `out`
параметр, использования которых хотелось бы по возможности избежать)
3. Метод `ValidateToken` возвращает `ClaimsPrincipal`, хотя нам было бы удобнее работать с payload'ом токена в формате
`IDictionary<string, string>`
4. В случае невалидности токена, мы получаем exception, из-за чего мы вынуждены использовать `try-catch`: проблема в
том, что весь последующий код также должен быть ключён в блок `try`, а реакция на невалидность токена переносится в
самый конец метода, где её можно просто не заметить.

Создадим метод `ParseToken` в нашем helper'е, исправив все вышеперечисленные проблемы:
```csharp
public IDictionary<string, string> ParseToken(string token, byte[] jwtSecret)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    tokenHandler.InboundClaimTypeMap.Clear();
    tokenHandler.OutboundClaimTypeMap.Clear();
    
    var tokenValidationParameters = new TokenValidationParameters
    {
        RequireSignedTokens = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtSecret),

        ValidateAudience = false,
        ValidateIssuer = false,

        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    try
    {
        var claimsPrincipal = tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
        return claimsPrincipal.Claims.ToDictionary(claim => claim.Type, claim => claim.Value);
    }
    catch (Exception)
    {
        return null;
    }
}
```

## Выпуск токенов

Теперь мы можем вынести логику создания токена. Начнём с выделения проблем:
1. Нужно указывать большое количество повторяющихся параметров, например `Expires` и `SigningCredentials`
2. Нужно передавать дату протухания токена, хотя нам было бы удобнее работать его лайфтаймом в формате `TimeSpan`
3. Неудобно описывать сlaim'ы – проще было бы работать с `IDictionary<string, object>`
4. Выпуск токена выполняется в 3 шага (описание payload, создание токена, encoding токена) – их количесто можно
сократить до всего лишь одного.

Создадим метод `IssueToken`, исправляя проблемы, перечисленные выше:
```csharp
public string IssueToken(IDictionary<string, object> claims, TimeSpan lifetime, byte[] secret)
{
    var descriptor = new SecurityTokenDescriptor
    {
        Claims = claims,
        Expires = DateTime.UtcNow.Add(lifetime),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(secret),
            SecurityAlgorithms.HmacSha256Signature)
    };
    
    var handler = new JwtSecurityTokenHandler();
    var tokenObject = handler.CreateToken(descriptor);
    var encodedToken = handler.WriteToken(tokenObject);

    return encodedToken;
}
```

Мы также можем обратить внимание на то, что мы никогда не выпускаем только 1 токен – мы создаём сразу пару. Добавим
метод `IssueTokenPair`:
```csharp
public TokenPair IssueTokenPair(Guid userId, Guid rtId, int atLifetime, int rtLifetime, byte[] secret)
{
    var accessToken = IssueToken(
        new Dictionary<string, object>
        {
            {"sub", userId}
        },
        TimeSpan.FromMinutes(atLifetime),
        secret);
    
    var refreshToken = IssueToken(
        new Dictionary<string, object>
        {
            {"sub", userId},
            {"jti", rtId}
        },
        TimeSpan.FromDays(rtLifetime),
        secret);

    return new TokenPair(accessToken, refreshToken);
}

public record TokenPair(string AccessToken, string RefreshToken);
```

> **Note 1**: `TokenPair` это
[record](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record). Фактически, эта запись
эквивалентна объявлению класса с 2мя `readonly` полями и конструктором, который присваивает им значения.

> **Note 2**: Я использовал сокращённые названия параметров `rtId` (Refresh Token Id), `atLifetime` (Access Token
Lifetime) и `rtLifetime` (Refresh Token Lifetime) потому, что я достаточно часто упоминаю эти сокращения в курсе. Не
используйте непопулярные сокращения, если это может ввести в заблуждение членов вашей команды.

## Рефакторинг сервиса

Мы создали сервис `JwtTokenHelper`, который значительно упростит код `UserController`'а, однако, мы ещё не закончили –
если мы посмотрим на наш сервис, то увидим довольно много проблем:
1. `JwtSecurityTokenHandler` инициализируется и настраивается несколько раз – лучше вынести его в поле сервиса
2. Каждый метод принимают настройки JWT Auth, хотя их можно было передать 1 раз в конструктор
3. Метод `IssueToken` не будет использоваться извне, так что модификатор `public` нужно заменить на `private`.

В результате имеем:
```csharp
public class JwtTokenHelper
{
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly SymmetricSecurityKey _securityKey;
    private readonly TimeSpan _accessTokenLifetime;
    private readonly TimeSpan _refreshTokenLifetime;

    public JwtTokenHelper(IConfiguration configuration)
    {
        _tokenHandler = new JwtSecurityTokenHandler();
        _tokenHandler.InboundClaimTypeMap.Clear();
        _tokenHandler.OutboundClaimTypeMap.Clear();

        var jwtSecret = Encoding.ASCII.GetBytes(configuration["JwtAuth:Secret"]);
        _securityKey = new SymmetricSecurityKey(jwtSecret);

        var accessTokenLifetimeInMinutes = int.Parse(configuration["JwtAuth:AccessTokenLifetime"]);
        _accessTokenLifetime = TimeSpan.FromMinutes(accessTokenLifetimeInMinutes);
        
        var refreshTokenLifetimeInDays = int.Parse(configuration["JwtAuth:RefreshTokenLifetime"]);
        _refreshTokenLifetime = TimeSpan.FromMinutes(refreshTokenLifetimeInDays);
    }
    
    public IDictionary<string, string> ParseToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _securityKey,
            ValidateAudience = false,
            ValidateIssuer = false,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var claimsPrincipal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            return claimsPrincipal.Claims.ToDictionary(claim => claim.Type, claim => claim.Value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public TokenPair IssueTokenPair(Guid userId, Guid refreshTokenId)
    {
        var accessToken = IssueToken(
            new Dictionary<string, object>
            {
                {"sub", userId}
            },
            _accessTokenLifetime);
        
        var refreshToken = IssueToken(
            new Dictionary<string, object>
            {
                {"sub", userId},
                {"jti", refreshTokenId}
            },
            _refreshTokenLifetime);

        return new TokenPair(accessToken, refreshToken);
    }

    private string IssueToken(IDictionary<string, object> claims, TimeSpan lifetime)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.HmacSha256Signature)
        };
        
        var tokenObject = _tokenHandler.CreateToken(descriptor);
        var encodedToken = _tokenHandler.WriteToken(tokenObject);

        return encodedToken;
    }

    public record TokenPair(string AccessToken, string RefreshToken);
}
```

## Рефакторинг `UserController`'а

Воспользуемся сервисом `JwtTokenHelper` в `UserController`'e:
```csharp
public class UserController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly IConfiguration _configuration;
    private readonly JwtTokenHelper _tokenHelper;

    public UserController(DatabaseContext context, IConfiguration configuration, JwtTokenHelper tokenHelper)
    {
        _context = context;
        _configuration = configuration;
        _tokenHelper = tokenHelper;
    }
    
    [HttpPost("authenticate")]
    public async Task<IActionResult> AuthenticateUser([FromBody] AuthenticateUserDto authenticateDto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(user => user.Username == authenticateDto.Username);
        if (user is null)
        {
            return NotFound();
        }
        if (!BCrypt.Verify(authenticateDto.Password, user.Password))
        {
            return Conflict("Incorrect password.");
        }

        var refreshTokenLifetime = int.Parse(_configuration["JwtAuth:RefreshTokenLifetime"]);
        var refreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ExpirationTime = DateTime.UtcNow.AddDays(refreshTokenLifetime)
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        var tokenPair = _tokenHelper.IssueTokenPair(user.Id, refreshTokenEntity.Id);
        var tokenPairDto = new TokenPairDto
        {
            AccessToken = tokenPair.AccessToken,
            RefreshToken = tokenPair.RefreshToken
        };
        return Ok(tokenPairDto);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshTokenPair([FromBody] string refreshToken)
    {
        var refreshTokenClaims = _tokenHelper.ParseToken(refreshToken);
        if (refreshTokenClaims is null)
        {
            return BadRequest("Invalid refresh token was provided.");
        }
        
        var refreshTokenId = Guid.Parse(refreshTokenClaims["jti"]);
        var refreshTokenEntity = await _context.RefreshTokens.SingleOrDefaultAsync(rt => rt.Id == refreshTokenId);
        if (refreshTokenEntity is null)
        {
            return Conflict("Provided refresh token has already been used.");
        }

        _context.RefreshTokens.Remove(refreshTokenEntity);
        await _context.SaveChangesAsync();

        var userId = Guid.Parse(refreshTokenClaims["sub"]);
        var refreshTokenLifetime = int.Parse(_configuration["JwtAuth:RefreshTokenLifetime"]);
        var newRefreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExpirationTime = DateTime.UtcNow.AddDays(refreshTokenLifetime)
        };
        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        var tokenPair = _tokenHelper.IssueTokenPair(userId, refreshTokenEntity.Id);
        var tokenPairDto = new TokenPairDto
        {
            AccessToken = tokenPair.AccessToken,
            RefreshToken = tokenPair.RefreshToken
        };
        return Ok(tokenPairDto);
    }
}
```

> **Note**: Методы `GetCurrentUserInfo` и `RegisterNewUser` не были затронуты в ходе рефакторинга, так что я не включил
их в листинг кода выше. Тоже самое касается документационных анотаций и комментариев.

Также нам нужно добавить сервис в DI контейнер (чтобы он автоматически передавался в конструктор `UserController`). Для
этого в модуле `Program` добавим:
```csharp
builder.Services.AddScoped<JwtTokenHelper>();
```