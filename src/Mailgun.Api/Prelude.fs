[<AutoOpen>]
module internal Mailgun.Prelude

open NodaTime
open NodaTime.Text

type Instant with
  member x.RFC2822UTC() =
    let pattern = InstantPattern.CreateWithInvariantCulture "ddd d MMM yyyy HH:mm:ss '+0000'"
    pattern.Format x