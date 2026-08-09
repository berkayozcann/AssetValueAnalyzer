# Asset Value Analyzer

Asset Value Analyzer; aylık Varlık ve Yİ-ÜFE verilerini, Finmaks'tan alınarak
MSSQL'de tutulan USD/TRY kurlarıyla birleştiren ASP.NET Core MVC uygulamasıdır.
Kullanıcı sabit şirket şablonundaki XLSX dosyalarını yükler, rapor aralığını seçer ve nominal,
dolarizasyon ve enflasyon etkilerini 14 kolonlu aylık tabloda görüntüler.

## Güncel durum

Zorunlu kullanıcı akışının çalışan çekirdeği tamamlandı:

- Finmaks için typed `HttpClient`, doğrulanan options ve `CashChangeRate` mapping'i.
- EF Core Code First, MSSQL migration'ı ve idempotent kur insert/update akışı.
- Uygulama başlangıcında Aralık 2021'den bugüne backfill veya eksik aralık tamamlama.
- Varlık ve Endeks verileri için şirket örneklerinin sabit satır/sütun yapısına uyumlu XLSX parser'ları.
- Dosya boyutu, uzantı, içerik/şablon, tarih, sayı ve duplicate ay doğrulamaları.
- Geçerli dosyaları kullanıcı session'ında tutan rapor çalışma alanı.
- Tarih aralığı ve eksik ay doğrulaması.
- Aylık son iş günü USD/TRY `CashChangeRate` seçimi ve en fazla 10 günlük geri arama.
- `decimal` kullanan nominal, dolarizasyon ve enflasyon hesapları.
- KPI özeti ve şartnamedeki 14 kolonu gösteren gerçek Razor sonuç ekranı.
- Kontrollü production fixture'ı ve şirket Excel'indeki referans kur fixture'ıyla formül testleri.

Henüz tamamlanmayan ana işler:

- Hangfire ile uygulama açıkken periyodik kur kontrolü.
- Ayrı API projesinde güncel kur controller'ı ve response DTO sözleşmesi.
- SignalR ile son kontrol/kur değişikliği bilgisinin refresh olmadan güncellenmesi.
- Gerçek host üzerinde `WebApplicationFactory<Program>` smoke testleri.
- Temiz MSSQL kurulumu, Docker/çalıştırma yolu, CI ve son teslim kontrolleri.

## Kullanılan teknolojiler

- .NET 10 / ASP.NET Core 10 MVC
- Razor Views ve strongly typed view model'ler
- EF Core 10 Code First + MSSQL
- Typed `HttpClient` ile Finmaks ExchangeRates entegrasyonu
- ClosedXML ile XLSX okuma
- Tailwind CSS 4 local CLI build
- Vanilla JavaScript
- xUnit, SQLite tabanlı persistence/controller integration testleri

## Solution yapısı

```text
AssetValueAnalyzer.sln
src/
├── AssetValueAnalyzer.Domain
├── AssetValueAnalyzer.Application
├── AssetValueAnalyzer.Infrastructure
├── AssetValueAnalyzer.Web
└── AssetValueAnalyzer.Api
tests/
├── AssetValueAnalyzer.UnitTests
└── AssetValueAnalyzer.IntegrationTests
```

Bağımlılık yönü:

```text
Domain <- Application <- Web / Api
              ^
              |
        Infrastructure
```

- `Domain`: Kur entity'si ve framework'ten bağımsız temel kurallar.
- `Application`: Import sözleşmeleri, kur senkronizasyonu, rapor doğrulama ve hesaplama.
- `Infrastructure`: EF Core/MSSQL, Finmaks client ve XLSX parser implementasyonları.
- `Web`: MVC controller'ları, Razor Views, session çalışma alanı ve tarayıcı kodu.
- `Api`: Ayrı API host iskeleti; güncel kur endpointleri henüz eklenmedi.

Generic repository, MediatR, CQRS ve AutoMapper kullanılmaz. İş kuralları
controller veya Razor view içine taşınmaz.

## Çalışan akış

### 1. Kur başlangıç senkronizasyonu

```text
Web uygulaması başlar
→ ExchangeRateInitializationHostedService scope oluşturur
→ InitializeExchangeRatesService mevcut son kur tarihini okur
→ DB boşsa 2021-12-01'den bugüne, doluysa eksik aralığı ister
→ Finmaks typed client response'u normalize eder
→ EF Core aynı para çifti ve gün için insert/update/unchanged uygular
```

Başlangıç senkronizasyonu hata alırsa hata loglanır ve web hostu çalışmaya devam
eder. Periyodik kontrol henüz eklenmedi; bunun için Hangfire planlanıyor.

### 2. Dosya yükleme ve session

```text
POST Varlık/Endeks dosyası
→ 5 MB sınırı ve desteklenen uzantı kontrolü
→ sabit şirket şablonuna uygun XLSX parser
→ ortak aylık normalize model ve validation
→ geçerliyse kullanıcının session'ına kaydet
→ geçersizse yalnız ilgili dosyanın durumunu temizle ve 422 döndür
```

Yüklenen finansal veriler MSSQL'e yazılmaz. Kullanıcının normalize dosya verileri
ve tamamlanan raporu, iki saatlik in-memory session içinde tutulur. MSSQL'de
kalıcı olarak yalnız kur verileri bulunur.

### 3. Rapor oluşturma

```text
POST /reports/create
→ session'daki iki veri setini oku
→ tarih aralığını ve eksik ÜFE aylarını doğrula
→ her varlık ayı için ayın son hafta gününü bul
→ o tarihten en fazla 10 takvim günü gerideki USD/TRY CashChangeRate'i seç
→ 14 kolonlu finansal hesabı decimal ile yap
→ presentation sınırında Türkçe para/yüzde formatı uygula
→ sonucu session'a kaydet
→ /reports sonuç ekranına yönlendir
```

Arada ay bulunmayan varlık dosyalarında yalnız mevcut varlık ayları rapora girer;
gerçek önceki takvim ayı yoksa üç "önceki aya göre" değişim kolonu `—` gösterilir.
Diğer nominal, dolarizasyon ve enflasyonizasyon değerleri hesaplanmaya devam eder.

## Gereksinimler

- .NET SDK 10
- Erişilebilir MSSQL instance'ı
- Node.js 20 veya üzeri
- pnpm 11 (`package.json` şu anda `pnpm@11.16.0` kullanır)
- EF migration komutları için `dotnet-ef` 10

Sürümleri kontrol etmek için:

```bash
dotnet --version
dotnet ef --version
node --version
pnpm --version
```

## Secret ve veritabanı yapılandırması

Web ve API projeleri aynı development `UserSecretsId` değerini kullanır. Gerçek
Finmaks API key'i ve MSSQL parolası repository'ye yazılmaz.

Repository kökünde kendi MSSQL bilgilerinizi kullanarak:

```bash
dotnet user-secrets set "ConnectionStrings:AssetValueAnalyzer" "Server=localhost,1433;Database=AssetValueAnalyzer;User Id=sa;Password=<YOUR_PASSWORD>;TrustServerCertificate=True" --project src/AssetValueAnalyzer.Web
dotnet user-secrets set "Finmaks:ApiKey" "<YOUR_FINMAKS_API_KEY>" --project src/AssetValueAnalyzer.Web
```

Finmaks test adresi source-controlled `appsettings.json` içinde bulunur. API key
yalnız user-secrets veya environment variable üzerinden verilmelidir.

Environment variable karşılıkları:

```text
ConnectionStrings__AssetValueAnalyzer
Finmaks__ApiKey
```

## İlk kurulum

Repository kökünde:

```bash
cd src/AssetValueAnalyzer.Web
pnpm install --frozen-lockfile
pnpm run css:build
cd ../..

dotnet restore AssetValueAnalyzer.sln
dotnet build AssetValueAnalyzer.sln --no-restore
```

Migration'ı doğru MSSQL hedefini kontrol ettikten sonra uygulayın:

```bash
dotnet ef database update \
  --project src/AssetValueAnalyzer.Infrastructure \
  --startup-project src/AssetValueAnalyzer.Web
```

Bu komut `ExchangeRates` tablosunu ve
`(BaseCurrencyCode, ForeignCurrencyCode, RateDate)` unique indexini oluşturur.

## Uygulamayı çalıştırma

```bash
dotnet run --project src/AssetValueAnalyzer.Web/AssetValueAnalyzer.Web.csproj
```

Terminalde yazan HTTP/HTTPS adresini tarayıcıda açın.

CSS üzerinde çalışırken ayrı terminalde:

```bash
cd src/AssetValueAnalyzer.Web
pnpm run css:watch
```

## Ekranlar ve HTTP işlemleri

- `GET /`: Kur kartı, Varlık/Endeks yükleme kartları ve rapor tarih seçimi.
- `GET /reports`: Boş, taslak veya tamamlanmış rapor çalışma alanı.
- `POST /imports/assets/validate`: Varlık XLSX doğrulaması.
- `POST /imports/indices/validate`: Endeks XLSX doğrulaması.
- `POST /reports/validate-range`: Tarih aralığı ve veri kapsaması doğrulaması.
- `POST /reports/create`: Gerçek finansal rapor hesabı.

Web arayüzünden indirilebilen örnek veri dosyaları:

- `src/AssetValueAnalyzer.Web/wwwroot/samples/asset-values.xlsx`
- `src/AssetValueAnalyzer.Web/wwwroot/samples/producer-price-indices.xlsx`

Şirketin 9 Ağustos 2026 tarihli yazılı açıklamasına göre şartnamedeki XML ifadesi
hatalıdır. Kullanıcı yalnızca ekteki örneklerle aynı sabit format ve satır/sütun
yapısındaki XLSX dosyalarını yükleyecektir.

## Finansal hesap kuralları

- Rapor ayı seçilen aralığın son ayıdır.
- Dolarizasyon kuru USD (`BaseCurrencyCode = 1`) / TRY
  (`ForeignCurrencyCode = 56`) kaydının `CashChangeRate` alanıdır.
- Kur araması ayın son takvim gününden başlar; hafta sonu önceki cumaya alınır.
- Kur bulunamazsa en fazla 10 takvim günü geriye gidilir; yine yoksa rapor üretilmez.
- Ara hesaplar `decimal` hassasiyetinde tutulur, yuvarlama yalnız view model'de yapılır.
- Şirket Excel'indeki referans kur fixture'ı formül/kolon anlamını doğrular.
- Ayrı production fixture'ı uygulamanın `CashChangeRate` seçimiyle 14 değeri doğrular.

Şirket Excel'indeki referans kur değerleri production `CashChangeRate` değerleri
olmadığından iki test aynı end-to-end sonucu kanıtladığını iddia etmez.

## Testler

Tüm testleri çalıştırmak için:

```bash
dotnet test AssetValueAnalyzer.sln --no-restore
```

Son doğrulanan durum:

- Unit test: `41/41`
- Integration test: `55/55`
- Toplam: `96/96`
- Build: `0` hata, `0` uyarı

Testler; XLSX metadata/şablon/duplicate kurallarını, Finmaks
mapping'ini, EF upsert davranışını, session/controller akışını, son iş günü kur
seçimini ve 14 kolonlu finansal hesabı kapsar. Mevcut integration testleri SQLite
ve controller/servis seviyesindedir; gerçek HTTP pipeline için
`WebApplicationFactory<Program>` smoke testleri kalan işlerdendir.

## Bilinen kapsam sınırları

- Authentication/authorization şartnamede istenmediği için yoktur.
- Grafik, CSV/Excel export ve public deployment zorunlu kapsamda değildir.
- Session in-memory olduğu için uygulama yeniden başlatılırsa taslak ve rapor kaybolur.
- Çoklu instance deployment için distributed session store henüz yoktur.
- Hangfire, SignalR ve ayrı kur API endpointleri henüz tamamlanmamıştır.
- Import kapsamı şirketin sabit Varlık ve Endeks XLSX şablonlarıyla sınırlıdır.

## Güvenlik

- API key, gerçek connection string ve gerçek/hassas finansal veriler source control'e eklenmez.
- Finmaks `HttpClient` loglayıcıları query string içindeki API key'in loglanmaması
  için kaldırılmıştır.
- Dosyalar en fazla 5 MB olabilir; yalnız XLSX kabul edilir ve ZIP/içerik imzası doğrulanır.
