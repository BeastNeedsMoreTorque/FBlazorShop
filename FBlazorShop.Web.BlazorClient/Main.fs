﻿module FBlazorShop.Web.BlazorClient.Main

open Microsoft.AspNetCore.Components.Routing
open Microsoft.JSInterop
open Bolero.Remoting
open Elmish
open Services
open Orders
open Bolero

type Page =
    | Start
    | [<EndPoint "/">] Home of Model : PageModel<Home.Model>                  
    | [<EndPoint "/myOrders/{id}">] OrderDetail of id : int * model : PageModel<OrderDetail.Model>
    | [<EndPoint "/myOrders">] MyOrders of Model : PageModel<MyOrders.Model>
    | [<EndPoint "/checkout">] Checkout of Model : PageModel<Checkout.Model>
type Model = { 
    Page : Page
    Rendered : bool
    Token : string option
}

type Message = 
    | SetPage of Page
    | HomeMsg of Home.Message
    | MyOrdersMsg of MyOrders.Message
    | OrderDetailMsg of OrderDetail.Message
    | CheckoutMsg of Checkout.Message
    | Rendered
    | TokenRead of string
    | TokenNotFound
    | TokenSaved

let defaultPageModel remote = function
| MyOrders m -> Router.definePageModel m (MyOrders.init remote|> fst)
| Home m ->Router.definePageModel m (Home.init remote |> fst)
| OrderDetail (key, m) -> Router.definePageModel m (OrderDetail.init remote key |> fst)
| Checkout m -> Router.definePageModel m (Checkout.init remote None|> fst)
| Start -> ()
let router remote = Router.inferWithModel SetPage (fun m -> m.Page) (defaultPageModel remote)

let initPage init msg page =
    let model, cmd = init 
    let page = { Model = model } |> page
    {Page = page ; Rendered = false; Token = None}, Cmd.map msg cmd
    
let initOrderDetail remote key = 
    initPage (OrderDetail.init remote key) OrderDetailMsg (fun pageModel -> OrderDetail(key, pageModel))

let initMyOrders remote = 
    initPage (MyOrders.init remote) MyOrdersMsg MyOrders

let initCheckout remote order = 
    initPage (Checkout.init remote order) CheckoutMsg Checkout

let initHome remote = 
    initPage (Home.init remote) HomeMsg Home

let init = { Page = Start; Rendered = false; Token = None}, Cmd.none
 
 
let getToken (jsRuntime : IJSRuntime)  =
    let doWork () = 
        async{ 
            System.Console.WriteLine("too");
            let! res = 
                jsRuntime.InvokeAsync<string>("window.localStorage.getItem", "name")
                    .AsTask() 
                    |> Async.AwaitTask
            return
                match res with
                | null -> TokenNotFound
                | t -> TokenRead t
        }
    Cmd.ofAsync doWork () id raise

let update remote  (jsRuntime : IJSRuntime) message (model : Model)  : Model * Cmd<_>=
    let genericUpdate update subModel msg  msgFn pageFn =
        let subModel, cmd = update  msg subModel
        {model with Page = pageFn({ Model = subModel})}, Cmd.map msgFn cmd

    match message, model.Page with
    | Rendered, _ -> { model with Rendered = true}, getToken jsRuntime
    | SetPage(Home _), _ -> initHome remote 
    | SetPage(MyOrders _), _ -> initMyOrders remote
    | SetPage(OrderDetail (key, _)), _ -> initOrderDetail remote key 
    | SetPage(Checkout _), Checkout _ -> model, Cmd.none
    | SetPage(Checkout m), _ -> initCheckout remote m.Model.Order 
    | TokenRead t , _ ->  { model with Token = Some t }, Cmd.none
    | TokenNotFound , _ -> model, Cmd.none
    | MyOrdersMsg msg, MyOrders myOrdersModel ->
        genericUpdate MyOrders.update (myOrdersModel.Model) msg MyOrdersMsg MyOrders
   
    | HomeMsg (Home.Message.OrderMsg (CheckoutRequested o)),_  -> 
        let orderModel = Checkout.init remote (Some o) |> fst
        let init = { Model = orderModel } 
        model, init |> Checkout |> SetPage |> Cmd.ofMsg

    | HomeMsg msg, Home homeModel ->
        genericUpdate (Home.update remote)(homeModel.Model) msg HomeMsg Home
        
    | CheckoutMsg (Checkout.Message.OrderAccepted o), _  ->
        let orderModel = OrderDetail.init remote o |> fst
        let init = { Model = orderModel } 
        model, (o,init) |> OrderDetail |> SetPage |> Cmd.ofMsg

    | CheckoutMsg msg, Checkout model ->
         genericUpdate (Checkout.update remote)(model.Model) msg CheckoutMsg Checkout

    | OrderDetailMsg(OrderDetail.Message.OrderLoaded _), page 
        when (page |> function | OrderDetail _ -> false | _ -> true) -> 
        model, Cmd.none

    | OrderDetailMsg msg, OrderDetail(key, orderModel) ->
        genericUpdate 
            (OrderDetail.update remote)
            (orderModel.Model)
            msg 
            OrderDetailMsg 
            (fun pageModel -> OrderDetail(key, pageModel))
        
    | msg, model -> invalidOp   (msg.ToString() + " === " + model.ToString())
        
open Bolero.Html
open BoleroHelpers

type MainLayout = Template<"wwwroot\MainLayout.html">

let view  (js: IJSRuntime) ( model : Model) dispatch =
    let content =
        cond model.Page <| function
        | Home (model) ->
          Home.view model.Model (HomeMsg >> dispatch)
        | MyOrders model -> MyOrders.view model.Model (MyOrdersMsg >> dispatch)
        | OrderDetail(_ ,model) -> OrderDetail.view model.Model (OrderDetailMsg >> dispatch)
        | Checkout m -> Checkout.view m.Model (CheckoutMsg >> dispatch)
        | Start -> text "Loading ..."
    MainLayout()
        .GetPizzaLink(navLink NavLinkMatch.All 
            [attr.href "/"; attr.``class`` "nav-tab"] 
            [
                img [attr.src ("img/pizza-slice.svg" |> prependContent)] 
                div [] [text (model.Token |> function | Some x -> x | _ -> "empty")]
            ])
        .MyOrders(navLink NavLinkMatch.All 
            [attr.href "myOrders"; attr.``class`` "nav-tab"] 
            [
                img [attr.src ("img/bike.svg" |> prependContent)] 
                div [] [text "My Orders"]
            ])
        .Body(content)
        .Elt()

open System
open Bolero.Templating.Client

type MyApp() =
    inherit ProgramComponent<Model, Message>()
    override this.Program =
        let remote = this.Remote<PizzaService>()
        let update = update  remote (this.JSRuntime)
        let router = router remote
        Program.mkProgram (fun _ -> init) (update) (view  this.JSRuntime) 
        |> Program.withRouter router
        |> Program.withSubscription (fun _ -> Rendered |> Cmd.ofMsg)
#if DEBUG
        |> Program.withConsoleTrace
        |> Program.withErrorHandler 
            (fun (x,y) -> 
                Console.WriteLine("Error Message:" + x)
                Console.WriteLine("Exception:" + y.ToString()))
  //      |> Program.withHotReload
#endif

open Microsoft.AspNetCore.Components.Builder
open Microsoft.Extensions.DependencyInjection
open Bolero.Remoting.Client
type Startup() =
    member __.ConfigureServices(services: IServiceCollection) =
        services.AddRemoting()

    member __.Configure(app: IComponentsApplicationBuilder) =
        app.AddComponent<MyApp>("app")
