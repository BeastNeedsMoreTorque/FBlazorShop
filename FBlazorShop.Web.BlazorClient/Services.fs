﻿module FBlazorShop.Web.BlazorClient.Services

open Bolero.Remoting
open FBlazorShop.App.Model
open Common

type public PizzaService = 
    {
        getSpecials : unit -> Async<PizzaSpecial list>
        getToppings : unit -> Async<Topping list>
        getOrders : string -> Async<Order list>
        getOrderWithStatuses : string -> Async<OrderWithStatus list>
        getOrderWithStatus : string * int -> Async<OrderWithStatus option>
        placeOrder : string * Order -> Async<int>
        signIn : string * string -> Async<Result<Authentication,string>>
    }
    interface IRemoteService with
        member __.BasePath = "/pizzas"