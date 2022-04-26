# Переход от Minimal API к полноценым контроллерам


## Содержание

1. [Проблема Minimal API](#Проблема-Minimal-API)
2. [Создание контроллера](#Создание-контроллера)
3. [Изменение конфигурации приложения](#Изменение-конфигурации-приложения)


## Проблема Minimal API

В предыдущем уроке мы реализовали CRUD endpoint'ы используя подход Minimal API. Данный подход позволяет нам реализовать
простые небольшие API, однако, очень быстро наступает момент, когда пользоваться этим подходом становится просто больно.
Если мы продолжим писать весь код в 1 файл, то в какой-то момент заметим, что по этому файлу очень сложно
ориентироваться (если вы в этом сомневаетесь, то можете добавить ещё несколько CRUD операций для, например, пользователя
или компании, которая организует митап).

Решить данную проблему очень просто – нужно перестать использовать Minimal API и перейти на использование контроллеров.
Контроллер – специальный класс, который представляет из себя набор endpoint'ов, как-либо связанных между собой по смыслу
(в нашем случае – все endpoint'ы внутри `MeetupController` будут отвечать за управление митапами).


## Создание контроллера

1. [Объявление класса](#Объявление-класса)
2. [Перенос списка митапов](#Перенос-списка-митапов)
3. [Перенос endpoint'ов](#Перенос-endpointов)

### Объявление класса

Создадим папку `Controllers` в корне проекта, а в ней – класс `MeetupController`. Этот класс должен быть:
- Публичным,
- Унаследован от класса `ControllerBase`
- Помечен атрибутом [ApiController].
Это всё необходимые требования для создания контроллера. Также, пометим этот контроллер атрибутом `[Route("/meetups")]`,
чтобы не прописывать `/meetups` в url для каждого endpoint'a, как мы это делали с Minimal API.

В результате получим:
```csharp
[ApiController]
[Route("/meetups")]
public class MeetupController : ControllerBase
{
}
```

### Перенос списка митапов

Раньше митапы хранились в списке в модуле `Program` и сейчас нужно перенести их в контроллер. Также нужно будет внести 3
изменения:
1. Указать тип `ICollection<Meetup>` (ранее мы использовали `var` для автоматического определения типа, но `var`
не работает с полями классов)
2. Пометить поле как `static readonly` (`readonly` потому, что мы не будем пересоздавать список, а лишь
добвалять/удалять элементы, а `static` пока останется без пояснения)
3. Переименуем поле в `Meetups`, чтобы оно сооветствовало конвенциям именования (к сожалению мне не удалось найти
ссылку на конкретно это правило, однако Rider его рекомендует).

В результате получим:
```csharp
private static readonly ICollection<Meetup> Meetups =
    new List<Meetup>();
```

Также нужно перенести саму модель митапа (класс `Meetup`) в контроллер и пометить класс как `public`. Контроллер должен
выглядеть так:
```csharp
[ApiController]
[Route("/meetups")]
public class MeetupController : ControllerBase
{
    private static readonly ICollection<Meetup> Meetups =
        new List<Meetup>();

    public class Meetup
    {
        public Guid? Id { get; set; }
        public string Topic { get; set; }
        public string Place { get; set; }
        public int Duration { get; set; }
    }
}
```

### Перенос endpoint'ов

Рассмотрим процесс перевода Minimal API endpoint'a в Action (action'ом называется метод контроллера). Раньше endpoint
`PUT /meetups/{id}` выглядел так:
```csharp
app.MapPut("/meetups/{id:guid}", ([FromRoute] Guid id, [FromBody] Meetup updatedMeetup) =>
{
    var oldMeetup = meetups.SingleOrDefault(meetup => meetup.Id == id);

    // meetup with provided id does not exist
    if (oldMeetup is null)
    {
        return Results.NotFound();
    }

    oldMeetup.Topic = updatedMeetup.Topic;
    oldMeetup.Place = updatedMeetup.Place;
    oldMeetup.Duration = updatedMeetup.Duration;

    return Results.NoContent();
});
```

Его action-версия выглядит так:
```csharp
[HttpPut("{id:guid}")]
public IActionResult UpdateMeetup([FromRoute] Guid id, [FromBody] Meetup updatedMeetup)
{
    var oldMeetup = Meetups.SingleOrDefault(meetup => meetup.Id == id);

    // meetup with provided id does not exist
    if (oldMeetup is null)
    {
        return NotFound();
    }

    oldMeetup.Topic = updatedMeetup.Topic;
    oldMeetup.Place = updatedMeetup.Place;
    oldMeetup.Duration = updatedMeetup.Duration;

    return NoContent();
}
```

Основные отличия:
1. Вместо вызова уже созданного метода и передачи в него самописного анонимного метода, нам нуно определить новый метод
2. HTTP метод указывается с помощью атрибута `[HttpPut]` вместо `.MapPut`
3. Повторяющуюся часть url `/meetup` мы вынесли на уровень контроллера (`[Route("/meetup")]`), так что её больше не
нужно указывать не каждый раз
4. Часть url, которая раньше шла после общей части `/meetup` теперь указывается внутри атрибута `[HttpPut("{id:guid}")]`
5. Метод должен иметь имя и возвращать `IActionResult`
6. Теперь вместо вызова `Result.NotFound()`, `Results.NoContent()` и им подобных, можно просто указать название метода.

Остальные методы можете перевести сами (вся необходимая информация у вас уже есть). Результат должен выглядеть примерно
так:
```csharp
[ApiController]
[Route("/meetups")]
public class MeetupController : ControllerBase
{
    private static readonly ICollection<Meetup> Meetups =
        new List<Meetup>();

    [HttpPost]
    public IActionResult CreateMeetup([FromBody] Meetup newMeetup)
    {
        newMeetup.Id = Guid.NewGuid();
        Meetups.Add(newMeetup);

        return Ok(newMeetup);
    }

    [HttpGet]
    public IActionResult GetAllMeetups() =>
        Ok(Meetups);

    [HttpPut("{id:guid}")]
    public IActionResult UpdateMeetup([FromRoute] Guid id, [FromBody] Meetup updatedMeetup)
    {
        var oldMeetup = Meetups.SingleOrDefault(meetup => meetup.Id == id);

        // meetup with provided id does not exist
        if (oldMeetup is null)
        {
            return NotFound();
        }

        oldMeetup.Topic = updatedMeetup.Topic;
        oldMeetup.Place = updatedMeetup.Place;
        oldMeetup.Duration = updatedMeetup.Duration;

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public IActionResult DeleteMeetup([FromRoute] Guid id)
    {
        var meetupToDelete = Meetups.SingleOrDefault(meetup => meetup.Id == id);

        // meetup with provided id does not exist
        if (meetupToDelete is null)
        {
            return NotFound();
        }

        Meetups.Remove(meetupToDelete);
        return Ok(meetupToDelete);
    }

    public class Meetup
    {
        public Guid? Id { get; set; }
        public string Topic { get; set; }
        public string Place { get; set; }
        public int Duration { get; set; }
    }
}
```


### Изменение конфигурации приложения

Из модуля `Program.cs` нужно удалить все endpoint'ы, объявление списка митапов и саму модель митапа (т.к. всё) это мы
уже перенесли в `MeetupController`. Код должен выглядеть так:
```csharp
01:  var builder = WebApplication.CreateBuilder(args);
02:  
03:  builder.Services.AddEndpointsApiExplorer();
04:  builder.Services.AddSwaggerGen();
05:  
06:  var app = builder.Build();
07:  
08:  if (app.Environment.IsDevelopment())
09:  {
10:      app.UseSwagger();
11:      app.UseSwaggerUI();
12:  }
13:  
14:  app.Run();
```

Его нужно заменить на этот:
```csharp
01:  var builder = WebApplication.CreateBuilder(args);
02:  
03:  builder.Services.AddSwaggerGen();
04:  builder.Services.AddControllers();
05:  
06:  var app = builder.Build();
07:  
08:  if (app.Environment.IsDevelopment())
09:  {
10:      app.UseSwagger();
11:      app.UseSwaggerUI();
12:  }
13:  
14:  app.UseRouting();
15:  app.MapControllers();
16:
17:  app.Run();
```

Изменения:
1. Вместо `.AddEndpointsApiExplorer()` теперь вызывается `.AddControllers()` (регистрация контроллеров)
2. Были добавлены вызовы `.UseRouting()` и `.MapControllers()` (сопоставление action'ов и url).

Проверить работоспособность Web API можете самостоятельно.