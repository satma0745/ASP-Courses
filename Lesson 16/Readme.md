# Расширение API - запись на митапы

В этом уроке мы добавим первую реальную (не CRUD) фичу в наш Web API - запись пользователя на митап.


## Содержание

1. [Подготовка `entuty`'ей](#Подготовка-entutyей)
2. [Количество записавшихся на митап](#Количество-записавшихся-на-митап)
3. [Запись на митап](#Запись-на-митап)


## Подготовка `entuty`'ей

Начнём с того, что подготовим `MeetupEntity` и `UserEntity` к тому, чтобы хранить информацию о записях пользователей на
митап: нам нужно создать связь `many-to-many` между пользователями и митапами (т.к. каждый пользователь может быть
записан на несколько митапов, а на каждый митап может быть записано несколько пользователей). В EF Core все связи между
таблицами описываются с помощью навигационных свойств
([статья на эту тему](https://docs.microsoft.com/en-us/ef/core/modeling/relationships)):
```csharp
public class MeetupEntity
{
    public Guid Id { get; set; }
    public string Topic { get; set; }
    public string Place { get; set; }
    public int Duration { get; set; }
    
    public ICollection<UserEntity> SignedUpUsers { get; set; }
}

public class UserEntity
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    
    public ICollection<MeetupEntity> SignedUpMeetups { get; set; }
}
```

Поля `SignedUpUsers` и `SignedUpMeetups` являются навигационными: они представляют собой данные из таблицы,
соответствующей другому entity и не хранятся в БД.

Далее нам нужно описать связь в конфигурации с любой из сторон `many-to-many` связи (я буду показывать пример с
`MeetupEntityTypeConfiguration` просто потому что мне эта конфигурация кажется более подходящей):
```csharp
entity
    .HasMany(meetup => meetup.SignedUpUsers)
    .WithMany(user => user.SignedUpMeetups)
    .UsingEntity<Dictionary<string, object>>(
        "meetups_signed_up_users",
        joinEntity => joinEntity
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey("user_id")
            .HasConstraintName("fk_meetups_signed_up_users"),
        joinEntity => joinEntity
            .HasOne<MeetupEntity>()
            .WithMany()
            .HasForeignKey("meetup_id")
            .HasConstraintName("fk_users_signed_up_meetups"),
        joinEntity =>
        {
            joinEntity
                .HasKey("user_id", "meetup_id")
                .HasName("pk_meetups_signed_up_users");

            joinEntity
                .HasIndex("meetup_id")
                .HasDatabaseName("ix_meetups_signed_up_users_meetup_id");
        });
```

Расмотрим конфигурацию:
1. `.HasMany` и `.WithMany` описывают, что связь именно `many-to-many` (`one-to-many` будет описываться с помощью
`.HasMany` и `.WithOne`, а `one-to-one` - с поммощью `.HasOne` и `.WithOne`)
2. `.UsingEntity` указывает, какой entity отвечает за хранение данных о соотношении записей одной таблицы к другой
3. Использование `Dictionary<string, object>` в качестве типа entity позволяет нам избежать создания класса entity (у
нас будет таблица, но у не будет прямого контроля над данными в ней)
4. `meetups_signed_up_users` - название таблицы, которая будет создана для обеспечения связи `many-to-many`
5. Второй и третий параметры метода `.UsingEntity` отвечают за настройку связи `one-to-many` в обе стороны (мы могли бы
доверить EF Core их настройку, но тогда названия полей не были бы в `snake_case`)
6. Четвёртый парамер – описание самой таблицы `meetups_signed_up_users` – в данном случае мы просто хотим указать
кастомное название PK и индекса.

> **Note 1**: В большинстве случаев вам не нужно с первой попытки правильно настроить. Я создавал эту конфигурацию
следующим образом: сначала просто указал вызвал `.HasMany` `.WithMany` и `.UsingEntity` без указания параметров,
описанных в 4-6 пунктах, и создал миграцию; просмотрел миграцию и исправил в конфигурации всё то, что меня не устроило –
имена; удалил и пересоздал миграцию, убедился что в этот раз в миграции меня всё устраивает.

> **Note 2**: В данном случае в индексе `ix_meetups_signed_up_users_meetup_id` нет необходимости (т.к. оба поля таблицы
уже покрыты PK), но EF Core упорно пытается его создать, так что я решил просто оставить его и дать ему кастомное имя.

Можно создать, проверить и применить миграцию.


## Количество записавшихся на митап

Теперь нам нужно изменить `ReadMeetupDto` так, чтобы в ней также хранилась и информация о том, какое количество
пользователей уже записалось на митап (поле `SignedUp`):
```csharp
public class ReadMeetupDto
{
    public Guid Id { get; set; }
    public string Topic { get; set; }
    public string Place { get; set; }
    public int Duration { get; set; }
    public int SignedUp { get; set; }
}
```

Также нужно изменить конфигурацию в `MappingProfile`, чтобы из списка `SignedUpUsers` получать количество и записывать
его в поле `SignedUp`:
```csharp
CreateMap<MeetupEntity, ReadMeetupDto>()
    .ForMember(readDto => readDto.SignedUp, config => config.MapFrom(meetup => meetup.SignedUpUsers.Count));
```

Теперь в каждом action'е, который возвращает `ReadMeetupDto` нужно немного изменить код. Проблема в том, что EF не
загружает навигационные поля по стандарту (т.к. в таком случае он просто загружал бы половину БД при получении только 1й
записи). Так что везде, где мы получаем митапы из БД и возвращаем в виде `ReadMeetupDto` нужно загрузить информацию в
навигационное свойство `SignedUpUsers` используя `.Include`:
```csharp
[HttpGet]
public async Task<IActionResult> GetAllMeetups()
{
    var meetups = await _context.Meetups
        .Include(meetup => meetup.SignedUpUsers)
        .ToListAsync();
    
    var readDtos = _mapper.Map<ICollection<ReadMeetupDto>>(meetups);
    return Ok(readDtos);
}

[HttpDelete("{id:guid}")]
public async Task<IActionResult> DeleteMeetup([FromRoute] Guid id)
{
    var meetupToDelete = await _context.Meetups
        .Include(meetup => meetup.SignedUpUsers)
        .SingleOrDefaultAsync(meetup => meetup.Id == id);
    if (meetupToDelete is null)
    {
        return NotFound();
    }
    
    _context.Meetups.Remove(meetupToDelete);
    await _context.SaveChangesAsync();

    var readDto = _mapper.Map<ReadMeetupDto>(meetupToDelete);
    return Ok(readDto);
}
```

Исключения составляют только action'ы `CreateMeetup` (т.к. мы не получаем его из БД) и `UpdateMeetup` (т.к. мы не
возвращаем `ReadMeetupDto` и никак не обращаемся к `SignedUpUsers`).


## Запись на митап

Теперь осталось лишь реализовать сам action записи на митап. Сначала составим его текстовое описание:
1. Endpoint `POST /meetups/{id}/sign-up`
2. Он должен требовать аутентифицированного пользователя (иначе мы не знаем, кто вообще пытается записаться на митап)
3. Если митапа с указанным `id` не существует, то нужно возвращать `404 Not found` status code
4. Если этот пользователь уже записан на этот митап, то нужно возвращать `409 Conflict` status code
5. При успешной записи на митап нужно возвращать `200 Ok` status code

Резализуем выше описанные требования:
```csharp
[HttpPost("{id:guid}/sign-up")]
[Authorize]
public async Task<IActionResult> SignUpForMeetup([FromRoute] Guid id)
{
    var subClaim = User.Claims.Single(claim => claim.Type == "sub");
    var currentUserId = Guid.Parse(subClaim.Value);

    var meetup = await _context.Meetups
        .Include(meetup => meetup.SignedUpUsers)
        .SingleOrDefaultAsync(meetup => meetup.Id == id);
    if (meetup is null)
    {
        return NotFound();
    }

    var isAlreadySigned = meetup.SignedUpUsers.Any(user => user.Id == currentUserId);
    if (isAlreadySigned)
    {
        return Conflict("You've already signed up for this meetup.");
    }

    var currentUser = await _context.Users.SingleAsync(user => user.Id == currentUserId);
    meetup.SignedUpUsers.Add(currentUser);
    await _context.SaveChangesAsync();

    return Ok();
}
```

Осталось лишь написать документацию, и можно запускать приложение и проверять его на коррекность работы.