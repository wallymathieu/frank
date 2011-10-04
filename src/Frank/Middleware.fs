﻿module Frank.Middleware

open System
open System.Collections.Generic
open Frank
open Frank.Collections

/// Logs the incoming request and the time to respond.
let log app = fun (req: IDictionary<string, obj>) -> async {
  let sw = System.Diagnostics.Stopwatch.StartNew()
  let! response = app req
  printfn "Received a %A request from %A. Responded in %i ms."
          req?RequestMethod req?RequestUri sw.ElapsedMilliseconds
  sw.Reset()
  return response }

/// Intercepts a request using the HEAD method and strips away the returned body from a GET response.
let head app = fun (req: IDictionary<string, obj>) -> async {
  if (req?RequestMethod :?> string) <> "HEAD" then
    return! app req
  else
    req?RequestMethod <- "GET"
    let! status, headers, _ = app req
    return status, headers, Seq.empty }

/// Intercepts a request and checks for use of X_HTTP_METHOD_OVERRIDE.
let methodOverride app =
  // Leave out POST, as that is the method we are overriding.
  let httpMethods = ["GET";"HEAD";"PUT";"DELETE";"OPTIONS";"PATCH"]
  fun (req: IDictionary<string, obj>) -> async {
    let methd = req?RequestMethod :?> string
    let headers = req?RequestHeaders :?> IDictionary<string, string>
    if methd <> "POST" || headers?CONTENT_TYPE <> "application/x-http-form-urlencoded" then
      return! app req
    else
      let! body = req?RequestBody :?> Async<_> |> Stream.readToEnd
      let form = parseForm body
      let m = if isNotNullOrEmpty form?_method then form?_method
              elif headers.ContainsKey("HTTP_X_HTTP_METHOD_OVERRIDE") then
                headers?HTTP_X_HTTP_METHOD_OVERRIDE
              else methd
      let httpMethod = m.ToUpperInvariant()
      if httpMethods |> List.exists ((=) httpMethod) then
        req?methodoverride_original_method <- m
        req?RequestMethod <- httpMethod
        req?RequestBody <- async {
          return Item(body, async { return Ended }) }
      return! app req }