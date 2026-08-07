# Asset Value Analyzer

ASP.NET Core MVC ile geliştirilen finansal varlık analizi uygulaması. Bu repository şu anda onaylanan kullanıcı arayüzünün çalışan ilk dilimini içerir. Ekranlardaki finansal değerler açıkça `Demo` veya `Örnek Veri` olarak işaretlenmiş sentetik verilerdir; gerçek kur senkronizasyonu, dosya importu ve finansal hesaplama sonraki dikey dilimlerde bağlanacaktır.

## Kullanılan teknolojiler

- .NET 10 / ASP.NET Core MVC
- Razor Views ve strongly typed view model'ler
- Tailwind CSS 4 (local CLI build)
- Az miktarda vanilla JavaScript

## Gereksinimler

- .NET SDK 10
- Node.js 20 veya üzeri
- pnpm 11

Sürümleri kontrol etmek için:

```bash
dotnet --version
node --version
pnpm --version
```

## İlk kurulum

Repository kökünde:

```bash
cd src/AssetValueAnalyzer.Web
pnpm install
pnpm run css:build
cd ../..
dotnet restore
dotnet build AssetValueAnalyzer.sln
```

## Uygulamayı çalıştırma

Repository kökünde:

```bash
dotnet run --project src/AssetValueAnalyzer.Web/AssetValueAnalyzer.Web.csproj
```

Terminalde yazan `http://localhost:...` veya `https://localhost:...` adresini tarayıcıda açın.

CSS üzerinde çalışırken ayrı bir terminalde:

```bash
cd src/AssetValueAnalyzer.Web
pnpm run css:watch
```

## Mevcut ekranlar

- `/`: Üç adımlı rapor oluşturma tasarım önizlemesi
- `/reports/sample`: Sentetik verilerle örnek rapor ve 14 kolonlu detay tablosu

## Güvenlik

Finmaks API anahtarı, gerçek connection string'leri ve şirket tarafından sağlanan çalışma dosyaları repository'ye eklenmez. Geliştirme secret'ları daha sonraki aşamada user-secrets veya environment variable üzerinden verilecektir.
