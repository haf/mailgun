module Mailgun.Api

open System
open System.IO
open System.Net.Mail
open NodaTime
open HttpFs.Client

type Configured =
  { apiKey : string }

type MailBody =
  | HtmlBody of string
  | TextBody of string
  | TextAndHtmlBody of string * string

type Message =
  { from        : MailAddress
    ``to``      : MailAddress list
    cc          : MailAddress list
    bcc         : MailAddress list
    /// always UTF8 encoded
    subject     : string
    /// always UTF8 encoded
    body        : MailBody
    attachments : File list }

type Header = string * string

type Var = string * string

type SendOpts =
  { domain         : string
    tag            : string option
    campaign       : string option
    dkim           : bool
    /// max +3 days
    deliveryTime   : Instant option
    testMode       : bool
    tracking       : bool
    trackingClicks : bool
    trackingOpens  : bool
    extraHeaders   : Header list
    extraVars      : Var list }

  static member Create(?domain : string) =
    { domain         = defaultArg domain "example.com"
      tag            = None
      campaign       = None
      dkim           = true
      deliveryTime   = None
      testMode       = false
      tracking       = true
      trackingClicks = false
      trackingOpens  = false
      extraHeaders   = []
      extraVars      = [] }

type ApiResponse<'T> =
  | ServerError of string * Response
  | ClientError of string
  | NotFoundResult of string
  | Result of Response
  //| PaginatedResult of 'T * PageState * HttpClient.Response
  | Timeout of Duration

module internal Impl =
  let BaseUri = "https://api.mailgun.net/v3"

  let resource (r : string) =
    Uri (String.Concat [ BaseUri; "/"; r.TrimStart('/') ])

  let collection domain (coll : string) =
    resource (String.Concat [ domain; "/"; coll.TrimStart('/') ])

  let mailgunRequest settings methd resource =
    createRequest methd resource
    |> withBasicAuthentication "api" settings.apiKey

  let getMailgunApiResponse (req : Request) =
    async {
      let! resp = getResponse req
      match resp.StatusCode with
      | x when x < 300 ->
        return Result resp
      | x when x >= 400 && x < 500 ->
        let! err = Response.readBodyAsString resp
        return ClientError ("Request Error from server: " + err)
      | _ ->
        let! err = Response.readBodyAsString resp
        return ServerError (err, resp)
    }

module Messages =
  open Chiron
  open Chiron.Operators
  open Impl

  type SendResponse =
    { message : string
      id      : string }

    static member FromJson (_ : SendResponse) =
      (fun m i ->
        { message = m
          id      = i })
      <!> Json.read "message"
      <*> Json.read "id"

    static member ToJson (resp : SendResponse) =
      Json.write "message" resp.message
      *> Json.write "id" resp.id

  let private generateSendBody settings m =
    let emailNameValue formControlName email =
      NameValue ("to", email.ToString())

    let optRaw nonPrefixName value =
      NameValue ("o:" + nonPrefixName, value)

    let opt nonPrefixName = function
      | None -> failwith "programming error"
      | Some x -> NameValue ("o:" + nonPrefixName, x)

    let optb nonPrefixName = function
      | true -> NameValue ("o:" + nonPrefixName, "yes")
      | false -> NameValue ("o:" + nonPrefixName, "no")

    BodyForm
      [ // data:
        yield NameValue ("from", m.from.ToString())
        yield! m.``to`` |> List.map (emailNameValue "to")
        yield! m.cc |> List.map (emailNameValue "cc")
        yield! m.bcc |> List.map (emailNameValue "bcc")
        yield NameValue ("subject", m.subject)
        match m.body with
        | TextBody text -> yield NameValue ("text", text)
        | HtmlBody html -> yield NameValue ("html", html)
        | TextAndHtmlBody (text, html) ->
          yield NameValue ("text", text)
          yield NameValue ("html", html)
        yield! m.attachments |> List.map(fun file -> FormFile ("attachment", file))

        // settings:
        if Option.isSome settings.tag then
          yield opt "tag" settings.tag
        if Option.isSome settings.campaign then
          yield opt "campaign" settings.campaign
        yield optb "dkim" settings.dkim
        if Option.isSome settings.deliveryTime then
          let instant = settings.deliveryTime |> Option.get
          yield optRaw "deliverytime" (instant.RFC2822UTC())
        if settings.testMode then yield optb "testmode" true
        yield optb "tracking" settings.tracking
        yield optb "tracking-clicks" settings.trackingClicks
        yield optb "tracking-opens" settings.trackingOpens
        for (headerName, headerValue) in settings.extraHeaders do
          yield NameValue ("h:" + headerName, headerValue)
        for (varName, varValue) in settings.extraVars do
          yield NameValue ("v:" + varName, varValue)
      ]

  let send config sendOpts (m : Message) =
    mailgunRequest config Post (collection sendOpts.domain "messages")
    |> withBody (generateSendBody sendOpts m)
    |> getMailgunApiResponse
