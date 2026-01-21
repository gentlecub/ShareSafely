# Testing - ShareSafely

Guia para ejecutar y escribir pruebas unitarias.

## Requisitos

- .NET 8.0 SDK
- IDE con soporte para xUnit (VS Code, Visual Studio, Rider)

## Ejecutar Tests

### Todos los tests

```bash
dotnet test
```

### Con detalles

```bash
dotnet test --verbosity normal
```

### Con cobertura

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Test especifico

```bash
dotnet test --filter "FullyQualifiedName~FileStorageServiceTests"
```

## Estructura del Proyecto

```
tests/ShareSafely.API.Tests/
├── Controllers/
│   ├── FilesControllerTests.cs
│   └── LinksControllerTests.cs
├── Services/
│   ├── FileStorageServiceTests.cs
│   ├── SasLinkServiceTests.cs
│   └── FileMetadataServiceTests.cs
├── Helpers/
│   └── TestDataFactory.cs
└── ShareSafely.API.Tests.csproj
```

## Stack de Testing

| Libreria | Uso |
|----------|-----|
| xUnit | Framework de tests |
| Moq | Mocking de dependencias |
| FluentAssertions | Assertions legibles |
| EF InMemory | Base de datos en memoria |
| Coverlet | Cobertura de codigo |

## Patrones Utilizados

### Arrange-Act-Assert (AAA)

```csharp
[Fact]
public async Task Upload_WithValidFile_ShouldReturnOk()
{
    // Arrange - Preparar datos y mocks
    var request = TestDataFactory.CreateFileUploadRequest();
    _serviceMock.Setup(...).ReturnsAsync(...);

    // Act - Ejecutar la accion
    var result = await _controller.Upload(request);

    // Assert - Verificar resultado
    result.Should().BeOfType<OkObjectResult>();
}
```

### Theory con InlineData

```csharp
[Theory]
[InlineData(".exe")]
[InlineData(".bat")]
[InlineData(".sh")]
public void ValidateFile_WithInvalidExtension_ShouldThrow(string ext)
{
    var file = TestDataFactory.CreateInvalidFile(ext);
    var act = () => ValidateFile(file);
    act.Should().Throw<InvalidOperationException>();
}
```

### Mocking con Moq

```csharp
// Setup
_serviceMock
    .Setup(s => s.GetByIdAsync(It.IsAny<Guid>()))
    .ReturnsAsync(expectedResponse);

// Verify
_serviceMock.Verify(
    s => s.UploadAsync(It.IsAny<FileUploadRequest>()),
    Times.Once);
```

### FluentAssertions

```csharp
// Basico
result.Should().NotBeNull();
result.Should().Be(expected);

// Colecciones
list.Should().HaveCount(3);
list.Should().Contain(item);

// Excepciones
act.Should().Throw<InvalidOperationException>()
    .WithMessage("*error*");

// Fechas
date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
```

## TestDataFactory

Usar siempre el factory para datos consistentes:

```csharp
// Crear archivo
var archivo = TestDataFactory.CreateArchivo();

// Crear enlace
var enlace = TestDataFactory.CreateEnlace(archivoId);

// Crear request
var request = TestDataFactory.CreateFileUploadRequest("doc.pdf");

// Crear archivo invalido
var invalid = TestDataFactory.CreateInvalidFile(".exe");

// Crear archivo muy grande
var oversized = TestDataFactory.CreateOversizedFile(150); // 150 MB
```

## CI/CD

Los tests se ejecutan automaticamente en:

- Push a `main` o `develop`
- Pull Requests a `main`

Si algun test falla, el deploy se detiene.

### GitHub Actions

```yaml
- name: Ejecutar Tests
  run: dotnet test --configuration Release
```

## Agregar Nuevos Tests

1. Crear archivo en la carpeta correspondiente
2. Heredar patron AAA
3. Usar TestDataFactory para datos
4. Mockear dependencias externas
5. Nombrar descriptivamente: `Metodo_Condicion_Resultado`

## Cobertura Minima Recomendada

| Componente | Minimo |
|------------|--------|
| Services | 80% |
| Controllers | 70% |
| Total | 75% |
