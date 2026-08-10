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
- Hangfire + MSSQL storage ile uygulama açıkken varsayılan 3 dakikada bir bugünün
  kurlarını idempotent biçimde yenileme.
- Başarılı Hangfire kontrolünden sonra SignalR bildirimi ve kur kartının sayfa
  yenilenmeden kontrollü HTTP refetch ile güncellenmesi.
- Ayrı ASP.NET Core Web API hostundan en güncel veya tarih/para koduyla filtrelenmiş
  kurları entity'den ayrılmış DTO ve `ProblemDetails` sözleşmesiyle sunma.
- Varlık ve Endeks verileri için şirket örneklerinin sabit satır/sütun yapısına uyumlu XLSX parser'ları.
- Dosya boyutu, uzantı, içerik/şablon, tarih, sayı ve duplicate ay doğrulamaları.
- Geçerli dosyaları kullanıcı session'ında tutan rapor çalışma alanı.
- Tarih aralığı ve eksik ay doğrulaması.
- Aylık son iş günü USD/TRY `CashChangeRate` seçimi ve en fazla 10 günlük geri arama.
- `decimal` kullanan nominal, dolarizasyon ve enflasyon hesapları.
- KPI özeti ve şartnamedeki 14 kolonu gösteren gerçek Razor sonuç ekranı.
- Kontrollü production fixture'ı ve şirket Excel'indeki referans kur fixture'ıyla formül testleri.
- `WebApplicationFactory<Program>` ile gerçek host, routing, Razor, anti-forgery,
  multipart model binding ve session cookie smoke testleri.

Henüz tamamlanmayan ana işler: temiz MSSQL kurulumu, Docker/çalıştırma yolu,
CI ve son teslim kontrolleri.

## Kullanılan teknolojiler

- .NET 10 / ASP.NET Core 10 MVC
- Razor Views ve strongly typed view model'ler
- EF Core 10 Code First + MSSQL
- Typed `HttpClient` ile Finmaks ExchangeRates entegrasyonu
- Hangfire 1.8 + MSSQL job storage
- ASP.NET Core SignalR + yerel `@microsoft/signalr` JavaScript client
- ClosedXML ile XLSX okuma
- Tailwind CSS 4 local CLI build
- Vanilla JavaScript
- xUnit, SQLite tabanlı integration testleri ve `WebApplicationFactory` HTTP smoke testleri

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
- `Infrastructure`: EF Core/MSSQL, Finmaks client, XLSX parser ve Hangfire job wiring'i.
- `Web`: MVC controller'ları, Razor Views, session çalışma alanı ve tarayıcı kodu.
- `Api`: Read-only güncel kur controller'ı, query validation, response DTO ve
  `ProblemDetails` sözleşmesi.

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
eder.

### 2. Periyodik kur senkronizasyonu

```text
Hangfire recurring scheduler her 3 dakikada bir job enqueue eder
→ Hangfire job için yeni bir DI scope oluşturur
→ ExchangeRateSynchronizationJob mevcut Application servisini çağırır
→ tarih parametresi verilmeden yalnız bugünün Finmaks kurları istenir
→ aynı kur değerleri duplicate üretmez; RetrievedAtUtc son başarılı çekişe yenilenir
→ değişen kur alanları varsa aynı business key üzerinde update edilir
→ başarılı işlem Application bildirim portunu çağırır
→ Web implementasyonu bağlı tarayıcılara SignalR tamamlanma olayı gönderir
→ tarayıcı `/exchange-rates/card` partial'ını yeniden okuyup yalnız kur kartını değiştirir
```

Periyot `ExchangeRateRecurringJob:IntervalMinutes` ayarıyla değiştirilebilir;
cron kararlılığı için değer 60'ı kalansız bölmelidir. Hangfire dashboard endpointi
bilinçli olarak açılmamıştır. Job üç otomatik retry ve 120 saniyelik concurrent
execution kilidi kullanır; kalıcı idempotency güvencesi MSSQL unique indexidir.

### 3. Dosya yükleme ve session

```text
POST Aylık Varlık/Yİ-ÜFE dosyası
→ 5 MB sınırı ve desteklenen uzantı kontrolü
→ sabit şirket şablonuna uygun XLSX parser
→ ortak aylık normalize model ve validation
→ geçerliyse kullanıcının session'ına kaydet
→ geçersizse yalnız ilgili dosyanın durumunu temizle ve 422 döndür
```

Yüklenen finansal veriler MSSQL'e yazılmaz. Kullanıcının normalize dosya verileri
ve tamamlanan raporu, iki saatlik in-memory session içinde tutulur. MSSQL'de
kalıcı olarak yalnız kur verileri bulunur.

### 4. Rapor oluşturma

```text
POST /reports/create
→ session'daki iki veri setini oku
→ tarih aralığını ve eksik Yİ-ÜFE aylarını doğrula
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

### 5. Ayrı kur API'si

```text
GET /api/exchange-rates/latest veya GET /api/exchange-rates
→ query string tarih/para kodu/limit validation
→ Application IExchangeRateReader portu
→ EF Core AsNoTracking ve doğrudan read model projection
→ latest isteğinde filtrelere uyan en son/istenen gün
→ tarihçe isteğinde başlangıç ve bitiş dahil aralık sorgusu
→ API response DTO mapping
→ eşleşme yoksa 404 ProblemDetails, geçersiz query'de 400 ValidationProblemDetails
```

API entity `Id` değerini veya EF entity'sini dışarı vermez. Tarihçe endpointinde
`startDate` ve `endDate` birlikte zorunludur; iki sınır da sorguya dahildir.
`limit` varsayılan 100, izin verilen aralık 1–200'dür. Swagger/OpenAPI paketi
şartname kapsamında gerekli olmadığı için eklenmemiş; endpoint sözleşmesi README
ve integration testlerinde belgelenmiştir. Ayrı API hostu Finmaks'a doğrudan
çıkmaz; yalnız MSSQL'de kalıcılaştırılmış kur verilerini okuduğu için Finmaks API
key'ine veya Hangfire/import servislerine bağımlı değildir.

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

Web ve API projeleri aynı development `UserSecretsId` değerini kullanır. İki host
da MSSQL connection string'ini kullanır; Finmaks API key'i yalnız kur
senkronizasyonunu yapan Web hostu için gereklidir. Gerçek Finmaks API key'i ve
MSSQL parolası repository'ye yazılmaz.

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
ExchangeRateRecurringJob__Enabled
ExchangeRateRecurringJob__IntervalMinutes
```

## İlk kurulum

Repository kökünde:

```bash
cd src/AssetValueAnalyzer.Web
pnpm install --frozen-lockfile
pnpm run assets:build
cd ../..

dotnet restore AssetValueAnalyzer.sln
dotnet build AssetValueAnalyzer.sln --no-restore
```

`assets:build` komutu; Tailwind CSS ve SignalR browser dosyasına ek olarak lisanslı
Inter/Manrope variable fontlarını da `wwwroot` altına kopyalar. Böylece arayüz
harici bir font CDN'ine ihtiyaç duymadan her makinede aynı tipografiyi kullanır.

Migration'ı doğru MSSQL hedefini kontrol ettikten sonra uygulayın:

```bash
dotnet ef database update \
  --project src/AssetValueAnalyzer.Infrastructure \
  --startup-project src/AssetValueAnalyzer.Web
```

Bu komut `ExchangeRates` tablosunu ve
`(BaseCurrencyCode, ForeignCurrencyCode, RateDate)` unique indexini oluşturur.
Web uygulaması ilk kez başladığında Hangfire kendi operasyonel tablolarını aynı
veritabanındaki ayrı `HangFire` şemasında otomatik hazırlar.

## Uygulamayı çalıştırma

```bash
dotnet run --project src/AssetValueAnalyzer.Web/AssetValueAnalyzer.Web.csproj
```

Terminalde yazan HTTP/HTTPS adresini tarayıcıda açın.

Ayrı API hostunu çalıştırmak için:

```bash
dotnet run --project src/AssetValueAnalyzer.Api/AssetValueAnalyzer.Api.csproj
```

API hostu yalnız `ConnectionStrings:AssetValueAnalyzer` yapılandırmasını ister;
Finmaks API key'i olmadan veritabanındaki mevcut kur kayıtlarını sunabilir.
Periyodik kur senkronizasyonunu Web hostundaki Hangfire yürütür; API aynı MSSQL
veritabanındaki kayıtları salt okunur biçimde sunar. Bu nedenle kurların periyodik
olarak yenilenmesi için tarayıcının değil, Web sunucu prosesinin çalışıyor olması
gerekir.

CSS üzerinde çalışırken ayrı terminalde:

```bash
cd src/AssetValueAnalyzer.Web
pnpm run css:watch
```

## Ekranlar ve HTTP işlemleri

- `GET /`: Kur kartı, Aylık Varlık Verisi/Yİ-ÜFE Endeks Verisi yükleme alanları ve finansal etki analizi akışı.
- `GET /exchange-rates/card`: SignalR bildirimi sonrası yeniden okunan kur kartı partial'ı.
- `/hubs/exchange-rates`: Yalnız senkronizasyon tamamlanma bildirimi taşıyan SignalR hub'ı.
- `GET /reports`: Boş, taslak veya tamamlanmış rapor çalışma alanı.
- `POST /imports/assets/validate`: Varlık XLSX doğrulaması.
- `POST /imports/indices/validate`: Endeks XLSX doğrulaması.
- `POST /reports/validate-range`: Rapor dönemi ve veri kapsaması doğrulaması.
- `POST /reports/create`: Finansal etki analizini hesaplayıp raporu oluşturma.
- `GET /api/exchange-rates/latest`: Ayrı API hostundaki güncel kur listesi.
- `GET /api/exchange-rates`: Ayrı API hostundaki tarih aralıklı kur listesi.

Güncel/tek-gün endpointi query parametreleri:

- `rateDate=YYYY-MM-DD`: Belirli kur günü; verilmezse filtrelere uyan en son gün.
- `baseCurrencyCode`: Opsiyonel baz para kodu.
- `foreignCurrencyCode`: Opsiyonel karşı para kodu.
- `limit`: 1–200; varsayılan 100.

Örnek USD/TRY çağrısı:

```text
GET /api/exchange-rates/latest?baseCurrencyCode=1&foreignCurrencyCode=56&limit=1
```

Tarihçe endpointi query parametreleri:

- `startDate=YYYY-MM-DD`: Zorunlu başlangıç günü.
- `endDate=YYYY-MM-DD`: Zorunlu bitiş günü.
- `baseCurrencyCode`, `foreignCurrencyCode` ve `limit`: Opsiyonel filtreler.

Örnek USD/TRY tarihçe çağrısı:

```text
GET /api/exchange-rates?startDate=2026-08-01&endDate=2026-08-09&baseCurrencyCode=1&foreignCurrencyCode=56&limit=200
```

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

- Unit test: `43/43`
- Integration test: `91/91`
- Toplam: `134/134`
- Build: `0` hata, `0` uyarı

Testler; XLSX metadata/şablon/duplicate kurallarını, Finmaks
mapping'ini, EF upsert davranışını, session/controller akışını, SignalR notifier/hub
wiring'ini, eksik tarihsel kur kapsamının yeniden backfill edilmesini, son iş günü
kur seçimini ve 14 kolonlu finansal hesabı kapsar. HTTP smoke testleri;
ana sayfanın açılmasını, anti-forgery reddini, eksik dosya hata sözleşmesini ve
başarılı XLSX upload'ının session cookie ile sonraki isteğe taşınmasını doğrular.
İndirilebilir iki örneğin otomatik rapor döneminde birlikte çalışması ayrıca
doğrulanır. Tam akış testi iki XLSX yüklemesinden hesaplanan iki satırlı Razor sonuç
tablosuna kadar gerçek MVC pipeline'ını çalıştırır. Hangfire testleri; 3 dakikalık
cron/options doğrulamasını, enabled/disabled DI wiring'ini ve job'ın tarih aralığı
vermeden yalnız güncel kur senkronizasyonunu çağırmasını kapsar. SignalR testleri
Web hostunun gerçek notifier'ı kullandığını, hub negotiate route'unu ve refetch
partial sözleşmesini doğrular.
Dokuz ayrı API HTTP testi; DTO sözleşmesini, tek-gün/aralık filtrelerini, model
binding davranışını ve
`200`, `400`, `404`, `500` cevaplarını gerçek API pipeline'ında doğrular. Gerçek
API test hostu boş Finmaks anahtarıyla çalıştırılarak yalnız read-only MSSQL
bağımlılıklarının kaydedildiği ayrıca doğrulanır. Gerçek
MSSQL smoke kontrolü en güncel gün için 20 DTO, USD/TRY filtresi için tek kayıt
ve bulunmayan tarih için `404 application/problem+json` üretmiştir. Tarihçe smoke
kontrolü 8–9 Ağustos USD/TRY kayıtlarını iki sınır dahil ve yeniden eskiye sıralı
döndürmüş; eksik bitiş tarihi SQL çalıştırılmadan `400` üretmiştir.

## Bilinen kapsam sınırları

- Authentication/authorization şartnamede istenmediği için yoktur.
- Grafik, CSV/Excel export ve public deployment zorunlu kapsamda değildir.
- Session in-memory olduğu için uygulama yeniden başlatılırsa taslak ve rapor kaybolur.
- Çoklu instance deployment için distributed session store henüz yoktur.
- Import kapsamı şirketin sabit Varlık ve Endeks XLSX şablonlarıyla sınırlıdır.

## Güvenlik

- API key, gerçek connection string ve gerçek/hassas finansal veriler source control'e eklenmez.
- Finmaks `HttpClient` loglayıcıları query string içindeki API key'in loglanmaması
  için kaldırılmıştır.
- Dosyalar en fazla 5 MB olabilir; yalnız XLSX kabul edilir ve ZIP/içerik imzası doğrulanır.
