using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.MapPost("/products", async ([FromBody] ProductDTO product, [FromServices] AppDbContext db) =>
{
    await db.Products.AddAsync(new Product
    {
        Name = product.Name,
        Price = product.Price
    });
    await db.SaveChangesAsync();
});

app.MapGet("/products", async ([FromServices] AppDbContext db) =>
{
    return await db.Products.AsNoTracking().ToListAsync();
});

app.MapDelete("/products/{id}", async Task<IResult> (int id, [FromServices] AppDbContext db) =>
{
    var productToBeDeleted = await db.Products.FindAsync(id);

    if (productToBeDeleted is null) return TypedResults.NotFound();

    db.Products.Remove(productToBeDeleted);

    await db.SaveChangesAsync();

    return TypedResults.NoContent();
});

app.MapGet("/products/{id}", async Task<IResult> (int id, [FromServices] AppDbContext db) =>
{
    var productToBeUpdated = await db.Products.FindAsync(id);

    if (productToBeUpdated is null) return TypedResults.NotFound();

    return TypedResults.Ok(new
    {
        data = productToBeUpdated,
        Message = $"Successfully retrieved the product with ID of {id}"
    });
});

app.Run();
