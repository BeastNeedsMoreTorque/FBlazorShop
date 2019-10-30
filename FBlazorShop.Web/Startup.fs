namespace FBlazorShop.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open FBlazorShop.EF
open Bolero.Templating.Server
open Bolero.Remoting.Server
open FBlazorShop
type Startup() =
    member _.ConfigureServices(services: IServiceCollection) =

        services
        #if DEBUG
            .AddHotReload(templateDir = "../FBlazorShop.Web.BlazorClient")
        #endif
            .AddRemoting<Services.PizzaService>() 
            .AddEF("Data Source=pizza.db") 
            .SetupServices()
            .AddMvc()
            .AddRazorRuntimeCompilation() |> ignore
           
        services.AddServerSideBlazor()|> ignore

        

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =

        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        app
            .UseRemoting()
            .UseRouting()
            .UseStaticFiles() 
            .UseEndpoints(fun endpoints ->
                endpoints.MapBlazorHub() |> ignore
#if DEBUG
                endpoints.UseHotReload()
#endif
                endpoints.MapFallbackToPage("/_Host") |> ignore
            ) |> ignore
