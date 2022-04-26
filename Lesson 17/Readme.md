# Рефакторинг: Startup и Extension-методы

## Содержание

1. [Использование `Startup`](#Использование-Startup)
2. [Extension методы](#Extension-методы)


## Использование `Startup`

В данный момент весь конфигурационный код Web API приложения находится в модуле `Program`. Данный подход имеет некоторые
ограничения и не очень удобен в использовании, так что мы разделим конфигурацию на 4 части:
1. Штатная точка входа (запуска) приложения - `Program.Main`)
2. Точка входа для EF Core миграций - `Program.CreateHostBuilder`)
3. Настройка сервисов - `Statrup.ConfigureServices`
4. Настройка пайплайна (middlewares) - `Startup.Configure`.

Начнём с того, что создадим класс `Startup` в корне проекта, проинджектим в него конфигурацию и создадим 2 метода:
1. `void ConfigureServices(IServiceCollection)`
2. `void Configure(IApplicationBuilder, IWebHostEnvironment)`.

В первом методе опишем все сервисы, которые настраивались в модуле `Program` (вызовы `builder.Services.Add...`). Во
втором методе опишем все настройки пайплайна обработки запроса, т.е. регистрацию всех middleware (вызовы `app.Use...`):
```csharp
internal class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration) =>
        _configuration = configuration;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var projectDirectory = AppContext.BaseDirectory;
            
            var projectName = Assembly.GetExecutingAssembly().GetName().Name;
            var xmlFileName = $"{projectName}.xml";
            
            options.IncludeXmlComments(Path.Combine(projectDirectory, xmlFileName));
            
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Put Your access token here (drop **Bearer** prefix):",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            
            options.OperationFilter<OpenApiAuthFilter>();
        });
        
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtSecret = Encoding.ASCII.GetBytes(_configuration["JwtAuth:Secret"]);
                options.TokenValidationParameters = new TokenValidationParameters
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
                options.RequireHttpsMetadata = false;
        
                var tokenHandler = options.SecurityTokenValidators.OfType<JwtSecurityTokenHandler>().Single();
                tokenHandler.InboundClaimTypeMap.Clear();
                tokenHandler.OutboundClaimTypeMap.Clear();
            });
        
        services.AddDbContext<DatabaseContext>(options =>
        {
            var connectionString = _configuration.GetConnectionString("PostgreSQL");
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<JwtTokenHelper>();
        
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        
        services.AddControllers();
    }

    public void Configure(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            application.UseSwagger();
            application.UseSwaggerUI();
        }

        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
```

Вот и настал момент разобрать понятие и устройсво DI в ASP. **D**ependency **I**njection позволяет
зарегистрировать сервис в контейнере, после чего его можно получить в другом сервисе: в стандартном DI для ASP получение
сервиса происходит в консрукторе. Для регистрации сервиса необхоимо в методе `ConfigureServices` на параметре типа
`IServiceCollection` вызвать один из следующих методов:
1. `.AddSingleton<TService>` - будет создан 1 общий инстанс сервиса на всё приложение
2. `.AddScoped<TService>` - инстанс будет создаваться при каждом новом запросе к Web API
3. `.AddTransient<TService>` - инстанс будет создаваться при каждой попытке получить сервис.
Методы `.AddSwaggerGen`, `.AddAuthentication` и прочие также внутри вызывают эти 3 метода.

Теперь, когда мы вынесли настройку сервисов и пайплайна в `Startup`, нам нужно почистить `Program` и настроить его на
использование `Startup`:
```csharp
internal static class Program
{
    private static void Main(string[] arguments) =>
        CreateHostBuilder(arguments).Build().Run();

    private static IHostBuilder CreateHostBuilder(string[] arguments) =>
        Host.CreateDefaultBuilder(arguments)
            .ConfigureWebHostDefaults(builder => builder.UseStartup<Startup>());
}
```

Запустите приложение и убедитесь, что всё работает корректно.


## Extension методы

В c# есть такая фича, как [extension-методы](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/how-to-create-a-new-method-for-an-enumeration).
Мы используем их, чтобы упростить код в `Startup`.

### Метод `Configure`

Начнём с метода `Configure`. Заметим, что все методы, которые мы вызываем на типе `IApplicationBuilder` уже являются
extension-методами. Одним из популярных правил конфигурационных extension-методов является возвращение того же типа, на
котором был вызван метод, что позволяет chain'ить вызовы методов друг за другом:
```csharp
public void Configure(IApplicationBuilder application, IWebHostEnvironment environment)
{
    if (environment.IsDevelopment())
    {
        application
            .UseSwagger()
            .UseSwaggerUI();
    }

    application
        .UseRouting()
        .UseAuthentication()
        .UseAuthorization()
        .UseEndpoints(endpoints => endpoints.MapControllers());
}
```

Такой подход часто называется fluent-синтаксисом (EF Core Fluent API называется Fluent именно потому, что использует
этот формат). Сейчас мы не можем соединить цепочку вызовов, т.к. её разрыват if-statement. Однако, ничего не мешает нам
создать extension-метод, который будет выполнять код только при совпадении условия. Создадим папку `Extensions` в корне
проекта, а в ней класс `ApplicationBuilderExtensions`:
```csharp
internal static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder OnDevelopment(
        this IApplicationBuilder application,
        Action action,
        IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            action();
        }

        return application;
    }
}
```

В таком случае метод `Configure` может выглядеть следующим образом:
```csharp
public void Configure(IApplicationBuilder application, IWebHostEnvironment environment) =>
    application
        .OnDevelopment(() => application.UseSwagger().UseSwaggerUI(), environment)
        .UseRouting()
        .UseAuthentication()
        .UseAuthorization()
        .UseEndpoints(endpoints => endpoints.MapControllers());
```

**Note**: Использование fluent-синтаксиса опционально, так что если вам нравится обычный не chained подход, то вы можете
продолжать его использовать. Мне лично больше всего нравится их смесь, так что в source code'ах к проекту метод
`Configure` будет выглядеть следующим образом:
```csharp
public void Configure(IApplicationBuilder application, IWebHostEnvironment environment) =>
    application
        .OnDevelopment(environment, () =>
        {
            application.UseSwagger();
            application.UseSwaggerUI();
        })
        .UseRouting()
        .UseAuthentication()
        .UseAuthorization()
        .UseEndpoints(endpoints => endpoints.MapControllers());
```

### Метод `ConfigureServices`

Далее рассмотрим использование extension-методов в `ConfigureServices`. Создадим класс `DependencyInjectionExtensions`
со следующими extension-методами:
```csharp
internal static class DependencyInjectionExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            var projectDirectory = AppContext.BaseDirectory;
            
            var projectName = Assembly.GetExecutingAssembly().GetName().Name;
            var xmlFileName = $"{projectName}.xml";
            
            options.IncludeXmlComments(Path.Combine(projectDirectory, xmlFileName));
            
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Put Your access token here (drop **Bearer** prefix):",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            
            options.OperationFilter<OpenApiAuthFilter>();
        });

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddScoped<JwtTokenHelper>()
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtSecret = Encoding.ASCII.GetBytes(configuration["JwtAuth:Secret"]);
                options.TokenValidationParameters = new TokenValidationParameters
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
                options.RequireHttpsMetadata = false;

                var tokenHandler = options.SecurityTokenValidators.OfType<JwtSecurityTokenHandler>().Single();
                tokenHandler.InboundClaimTypeMap.Clear();
                tokenHandler.OutboundClaimTypeMap.Clear();
            });

        return services;
    }
    
    public static IServiceCollection AddDbContext(this IServiceCollection services, IConfiguration configuration) =>
        services.AddDbContext<DatabaseContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("PostgreSQL");
            options.UseNpgsql(connectionString);
        });
}
```

Я вынес в extension-методы только те настройки, которые занимали больше 1й строчки кода. В результате метод
`ConfigureServices` выглядит следующим образом:
```csharp
public void ConfigureServices(IServiceCollection services) =>
    services
        .AddSwagger()
        .AddJwtAuthentication(_configuration)
        .AddDbContext(_configuration)
        .AddAutoMapper(Assembly.GetExecutingAssembly())
        .AddControllers();
```

### Extension'ы для `IConfiguration`

Для получения конфигурации мы используем подобный код:
```csharp
var accessTokenLifetimeInMinutes = int.Parse(configuration["JwtAuth:AccessTokenLifetime"]);
_accessTokenLifetime = TimeSpan.FromMinutes(accessTokenLifetimeInMinutes);
```

Его также можно было бы вынести в extension'ы:
```csharp
internal static class ConfigurationExtensions
{
    public static string GetPostgreSqlConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString("PostgreSQL");
    
    public static SymmetricSecurityKey GetAuthSecret(this IConfiguration configuration)
    {
        var secret = configuration["JwtAuth:Secret"];
        var secretBytes = Encoding.ASCII.GetBytes(secret);
        return new SymmetricSecurityKey(secretBytes);
    }

    public static TimeSpan GetAccessTokenLifetime(this IConfiguration configuration)
    {
        var accessTokenLifetimeInMinutes = int.Parse(configuration["JwtAuth:AccessTokenLifetime"]);
        return TimeSpan.FromMinutes(accessTokenLifetimeInMinutes);
    }

    public static TimeSpan GetRefreshTokenLifetime(this IConfiguration configuration)
    {
        var refreshTokenLifetimeInDays = int.Parse(configuration["JwtAuth:RefreshTokenLifetime"]);
        return TimeSpan.FromMinutes(refreshTokenLifetimeInDays);
    }
}
```

> **Note**: Обновите код `DependencyInjectionExtensions` и `JwtTokenHelper` так, чтобы они использовали эти
extension-методы.