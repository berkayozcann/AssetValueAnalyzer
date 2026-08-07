# Asset Value Analyzer — Mimari Kararlar

## ADR-001: .NET ve sunum modeli

- Durum: Kabul edildi
- Karar: .NET 10, ASP.NET Core MVC, Razor Views, local Tailwind CSS ve az miktarda vanilla JavaScript.
- Gerekçe: Server-rendered dosya yükleme ve rapor akışı için sade, öğrenilebilir ve tekrar üretilebilir bir yapı.

## ADR-002: Proje sınırları

- Durum: Kabul edildi
- Karar: `Domain`, `Application`, `Infrastructure`, `Web`, `Api`, `UnitTests` ve `IntegrationTests` ayrı projeleridir.
- Gerekçe: Web ve ayrı kur API'si aynı iş kuralları ile persistence implementasyonunu paylaşırken Domain framework bağımsız kalır.

Bağımlılık yönü:

```text
Domain <- Application <- Web / Api
            ^
            |
      Infrastructure
```

Host projeleri Application ve Infrastructure servislerini composition root olan `Program.cs` içinde DI container'a bağlar.

## ADR-003: Veri erişimi

- Durum: Kabul edildi
- Karar: EF Core Code First + MSSQL kullanılacak. `DbContext`/`DbSet` yeterli olduğu sürece generic repository eklenmeyecek.
- Gerekçe: EF Core zaten unit-of-work, change tracking ve sorgulama sınırını sağlar; fazladan repository katmanı bu proje için somut fayda sağlamaz.

## ADR-004: Finansal veri hassasiyeti

- Durum: Kabul edildi
- Karar: Tutar, kur, endeks ve oranlarda `decimal` kullanılacak. Yuvarlama presentation sınırında yapılacak.
- Gerekçe: Finansal hesaplarda binary floating-point sapmalarını önlemek.

## ADR-005: Kur kaynağı ve seçilen alan

- Durum: Kabul edildi
- Karar: Finmaks response'unda USD `BaseCurrencyCode = 1`, TRY `ForeignCurrencyCode = 56`; production dolarizasyon hesabında `CashChangeRate` kullanılacak.
- Gerekçe: Şartname bu alanlardan birinin seçilmesini istiyor; seçim açık ve test edilebilir olmalıdır.

## ADR-006: Kur kaydı benzersizliği

- Durum: Entity/migration öncesi uygulanacak karar
- Karar: `(BaseCurrencyCode, ForeignCurrencyCode, RateDate)` veritabanında unique olacaktır.
- Gerekçe: Aynı gün ve para çifti tekrar senkronize edildiğinde duplicate yerine idempotent update yapılabilmesi.

## ADR-007: Dosya formatları

- Durum: Kabul edildi
- Karar: Şablonla uyumlu XLSX ve dokümante canonical XML iki ayrı adapter ile aynı normalize modele dönüştürülecek.
- Gerekçe: XLSX'in dahili XML yapısı business XML sözleşmesi değildir; validation ve hesaplama formattan bağımsız kalmalıdır.

## ADR-008: Zamanlanmış ve canlı işler

- Durum: Zorunlu akıştan sonraki bonus aşaması
- Karar: Kur senkronizasyon iş mantığı Application servisinde kalacak; Hangfire yalnız bu servisi zamanlayacak, SignalR yalnız tamamlanma bildirimi taşıyacak.
- Gerekçe: İş mantığının scheduler veya hub içine kilitlenmesini önlemek.

## ADR-009: Kapsam dışı erken soyutlamalar

- Durum: Kabul edildi
- Karar: Somut ihtiyaç çıkmadan MediatR, CQRS, AutoMapper, generic repository, event bus, Redis veya mikroservis eklenmeyecek.
- Gerekçe: 3–4 günlük mülakat projesinde doğruluk, izlenebilirlik ve anlatılabilirlik önceliklidir.
