﻿module Orders

open FBlazorShop.App.Model
open Elmish
open Bolero.Html
open FBlazorShop.Web.BlazorClient
open Services
open System

type Model = { Order : Order option; }

type OrderMsg = 
    | PizzaAdded of Pizza
    | PizzaRemoved of Pizza

let init () = 
    { Order = None; }, Cmd.none

let update ( state : Model) (msg : OrderMsg) : Model * Cmd<_> = 
    match msg with
    | PizzaAdded p ->
        let order = 
            match state.Order with
            | Some order -> { order with Pizzas = p :: [yield! order.Pizzas] } |> Some
            | _ ->
                {
                    OrderId = 0
                    UserId = ""
                    CreatedTime = System.DateTime.Now
                    DeliveryAddress = Unchecked.defaultof<_>
                    DeliveryLocation = Unchecked.defaultof<_>
                    Pizzas = [p]
                } |> Some
        { Order = order }, Cmd.none
    | PizzaRemoved tobeRemoved -> 
        let pizzas = [yield! state.Order.Value.Pizzas] |> List.filter (fun p -> System.Object.ReferenceEquals(tobeRemoved,p) |> not)
        if pizzas.Length = 0 then
            {state with Order = None}, Cmd.none
        else
        let order =
                { state.Order.Value with Pizzas = pizzas}
        { state with Order = Some order}, Cmd.none

let view (state : Model) dispatcher =
    let noOrder = div [attr.``class`` "empty-cart"] [text "Choose a pizza"; br[]; text "to get started"]

    let cartItem (pizza : Pizza) =
        div [attr.``class`` "cart-item"] [
            a [on.click (fun _ ->   pizza |> PizzaRemoved |> dispatcher); attr.``class`` "delete-item"] [text "x"]
            div [attr.``class`` "title"] [textf "%s\" %s" (pizza.Size.ToString()) pizza.Special.Name]
            ul [][
                forEach pizza.Toppings (fun t -> li [] [textf "+%s" t.Topping.Name])
            ]
            div [attr.``class`` "item-price"][
                text pizza.FormattedTotalPrice
            ]
        ]

    let upper = 
        cond state.Order <| function
        | Some o -> 
            cond (o.Pizzas.Count = 0) <| function
            | true -> noOrder
            | _ -> 
                div [attr.``class`` "order-contents"][
                    h2 [] [text "Your order"]
                    forEach o.Pizzas cartItem 
                ]
        | _ -> noOrder

    let lower =
        cond state.Order <| function
        | Some order ->
            div [attr.``class`` "order-total" ][
                text "Total:"
                span [attr.``class`` "total-price"] [text (order.FormattedTotalPrice)]
                button [attr.``class`` "btn btn-warning";][ text "Order >"]
            ]
        | _ -> empty

    span [] [ upper; lower]
