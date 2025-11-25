using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using HakiBaVuong.Data;
using System.Text;
using Microsoft.Extensions.FileProviders;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Services.AddDbContext<DataContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(Program).Assembly);


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://b1d5-14-167-171-205.ngrok-free.app")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
})
.AddCookie()
.AddGoogle(options =>
{
    var googleSection = builder.Configuration.GetSection("Authentication:Google");
    options.ClientId = googleSection["ClientId"];
    options.ClientSecret = googleSection["ClientSecret"];
    options.CallbackPath = "/signin-google";
})
.AddFacebook(facebookOptions =>
{
    var fbAuthNSection = builder.Configuration.GetSection("Authentication:Facebook");
    facebookOptions.AppId = fbAuthNSection["AppId"];
    facebookOptions.AppSecret = fbAuthNSection["AppSecret"];
    facebookOptions.CallbackPath = "/signin-facebook"; 
});



builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
});
var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
app.UseFileServer(new FileServerOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Images")),
    RequestPath = "/images",
    EnableDirectoryBrowsing = false
});
app.UseCors("AllowReactApp");
app.UseSession();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();