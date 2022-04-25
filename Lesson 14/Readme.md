# Использование AutoMapper

На данный момент в наших контроллерах огромное количество кода, где значения полей одного объекта копируются в другой,
на подобие этого:
```csharp
var readDto = new ReadUserDto
{
    Id = newUser.Id,
    DisplayName = newUser.DisplayName,
    Username = newUser.Username
};
```

В этом уроке мы исправим ситуацию с использованием
[AutoMapper](https://docs.automapper.org/en/latest/Getting-started.html).


## Содержание

1. [Принцип работы `AutoMapper`](#Принцип-работы-AutoMapper)
2. [Использование `AutoMapper` в `MeetupController`](#Использование-AutoMapper-в-MeetupController)
3. [Использование `AutoMapper` в `UserController`](#Использование-AutoMapper-в-UserController)
4. [Результат](#Результат)


## Принцип работы `AutoMapper`

Принцип работы `AutoMapper` довольно прост: мы объявляем `MappingProfile`'ы, в которых описываем, как привести значение
одного типа к другому (или тому же) типу. После мы можем вызвать метод `Map` и перенести значения с одного объекта на
другой, либо создать объект на основе другого.

Для начала работы с `AutoMapper` нам нужно утсановить библиотеку
[AutoMapper.Extensions.Microsoft.DependencyInjection](https://www.nuget.org/packages/AutoMapper.Extensions.Microsoft.DependencyInjection). Также нам
нужно добавить сервисы `AutoMapper` в DI контейнер, добавив следующий код в модуль `Program`:
```csharp
builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());
```


## Использование `AutoMapper` в `MeetupController`

Начнём переход на `AutoMapper` с того, что используем его в `MeetupController`'e. Сначала создадим файл
`MappingProfile.cs` в папке `Meetup` и объявим там класс `MeetupMappingProfile`. Этот класс нужно унаследовать от класса
`Profile`.

Далее нам нужено найти все моменты, когда мы копируем данные из одного типа в другой:
1. `CreateMeetup`: `CreateMeetupDto` -> `MeetupEntity`
2. `CreateMeetup`: `MeetupEntity` -> `ReadMeetupDto`
3. `GetAllMeetups`: `MeetupEntity` -> `ReadMeetupDto`
4. `UpdateMeetup`: `UpdateMeetupDto` -> `MeetupEntity`
5. `DeleteMeetup`: `MeetupEntity` -> `ReadMeetupDto`.

В конструкторе класса `MeetupMappingProfile` опишем все выше перечисленные маппини:
```csharp
public MeetupMappingProfile()
{
    CreateMap<MeetupEntity, ReadMeetupDto>();
    CreateMap<CreateMeetupDto, MeetupEntity>();
    CreateMap<UpdateMeetupDto, MeetupEntity>();
}
```

Теперь в `MeetupController` intject'нем `IMapper` и заменим все выше перечисленные участки кода на вызов метода `.Map`:
```csharp
public class MeetupController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly IMapper _mapper;

    public MeetupController(DatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateMeetup([FromBody] CreateMeetupDto createDto)
    {
        var newMeetup = _mapper.Map<MeetupEntity>(createDto);
        
        _context.Meetups.Add(newMeetup);
        await _context.SaveChangesAsync();

        var readDto = _mapper.Map<ReadMeetupDto>(newMeetup);
        return Ok(readDto);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAllMeetups()
    {
        var meetups = await _context.Meetups.ToListAsync();
        var readDtos = _mapper.Map<ICollection<ReadMeetupDto>>(meetups);
        return Ok(readDtos);
    }
    
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMeetup([FromRoute] Guid id, [FromBody] UpdateMeetupDto updateDto)
    {
        var oldMeetup = await _context.Meetups.SingleOrDefaultAsync(meetup => meetup.Id == id);
        if (oldMeetup is null)
        {
            return NotFound();
        }

        _mapper.Map(updateDto, oldMeetup);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
    
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMeetup([FromRoute] Guid id)
    {
        var meetupToDelete = await _context.Meetups.SingleOrDefaultAsync(meetup => meetup.Id == id);
        if (meetupToDelete is null)
        {
            return NotFound();
        }
        
        _context.Meetups.Remove(meetupToDelete);
        await _context.SaveChangesAsync();

        var readDto = _mapper.Map<ReadMeetupDto>(meetupToDelete);
        return Ok(readDto);
    }
}
```

> **Note**: Я не добавлял документационные комментарии и анотации в листинг кода выше, т.к. они не зименились.


## Использование `AutoMapper` в `UserController`

```csharp
internal class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<UserEntity, ReadUserDto>();
        CreateMap<RegisterUserDto, UserEntity>();
        CreateMap<JwtTokenHelper.TokenPair, TokenPairDto>();
    }
}

public class UserController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly IConfiguration _configuration;
    private readonly JwtTokenHelper _tokenHelper;
    private readonly IMapper _mapper;

    public UserController(
        DatabaseContext context,
        IConfiguration configuration,
        JwtTokenHelper tokenHelper,
        IMapper mapper)
    {
        _context = context;
        _configuration = configuration;
        _tokenHelper = tokenHelper;
        _mapper = mapper;
    }
    
    [HttpGet("who-am-i")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUserInfo()
    {
        var subClaim = User.Claims.Single(claim => claim.Type == "sub");
        var currentUserId = Guid.Parse(subClaim.Value);

        var currentUser = await _context.Users.SingleAsync(user => user.Id == currentUserId);

        var readDto = _mapper.Map<ReadUserDto>(currentUser);
        return Ok(readDto);
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> RegisterNewUser([FromBody] RegisterUserDto registerDto)
    {
        var usernameTaken = await _context.Users.AnyAsync(user => user.Username == registerDto.Username);
        if (usernameTaken)
        {
            return Conflict("Username already taken.");
        }

        var newUser = _mapper.Map<UserEntity>(registerDto);
        newUser.Password = BCrypt.HashPassword(registerDto.Password);
        
        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        var readDto = _mapper.Map<ReadUserDto>(newUser);
        return Ok(readDto);
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
        var tokenPairDto = _mapper.Map<TokenPairDto>(tokenPair);
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
        var tokenPairDto = _mapper.Map<TokenPairDto>(tokenPair);
        return Ok(tokenPairDto);
    }
}
```

> **Note**: Я не добавлял документационные комментарии и анотации в листинг кода выше, т.к. они не зименились.


## Результат

Запустите приложение и убедитесь в корректности работы. В ходе  рафакторинга за последние 2 урока мы добились того, что
кода в контроллерах стало гораздо меньше: самый большой метод, размер которого раньше превышал 100 строк стал меньше в 2
с лишним раза.
