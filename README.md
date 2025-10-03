# Clothing Store Backend API

## 🚀 .NET 8.0 Web API cho ứng dụng bán quần áo

### 📋 Tính năng
- ✅ CRUD operations cho Products
- ✅ RESTful API endpoints
- ✅ Entity Framework Core với SQL Server
- ✅ Swagger documentation
- ✅ CORS configuration
- ✅ Health checks
- ✅ Security headers
- ✅ Production ready

### 🛠️ Tech Stack
- **.NET 8.0** - Web API framework
- **Entity Framework Core** - ORM
- **SQL Server** - Database
- **Swagger** - API documentation
- **Docker** - Containerization

### 🚀 Quick Start

#### Development
```bash
# Restore dependencies
dotnet restore

# Update database
dotnet ef database update

# Run application
dotnet run
```

#### Docker
```bash
# Build image
docker build -t clothing-store-api .

# Run container
docker run -p 7000:80 clothing-store-api
```

### 📚 API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/products` | Get all products (with pagination) |
| GET | `/api/products/{id}` | Get product by ID |
| POST | `/api/products` | Create new product |
| PUT | `/api/products/{id}` | Update product |
| DELETE | `/api/products/{id}` | Delete product |
| GET | `/health` | Health check |

### 🔧 Configuration

#### Environment Variables
```env
ASPNETCORE_ENVIRONMENT=Development|Production
ConnectionStrings__DefaultConnection=your-connection-string
Cors__AllowedOrigins=https://your-frontend-domain.com
```

#### Database Connection
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ClothingStore;Trusted_Connection=true;"
  }
}
```

### 🚀 Deployment

#### Render.com
```bash
# Deploy to Render
# 1. Connect GitHub repository
# 2. Set environment variables
# 3. Deploy automatically
```

#### Fly.io
```bash
# Deploy to Fly.io
fly launch
fly deploy
```

#### Azure App Service
```bash
# Deploy to Azure
az webapp create --name your-app-name
az webapp deployment source config --name your-app-name --source-control github
```

### 📊 Database Schema

#### Product Entity
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### 🔒 Security Features
- CORS configuration
- Security headers
- HTTPS redirection (production)
- Input validation
- SQL injection protection

### 📈 Monitoring
- Health check endpoint: `/health`
- Structured logging
- Performance metrics
- Error tracking

### 🤝 Contributing
1. Fork the repository
2. Create feature branch
3. Commit changes
4. Push to branch
5. Create Pull Request

### 📄 License
MIT License
