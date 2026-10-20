namespace CoinquiLindo.Client
{
    public static class ErrorMessageProvider
    {
        public const string DefaultMessage = "Si è verificato un errore. Riprova.";

        // Keep aligned with the errorCode list in PROJECT_BIBLE.md §6
        private static readonly Dictionary<string, string> MessagesByCode = new(StringComparer.Ordinal)
        {
            ["House.NameRequired"] = "Inserisci un nome per la casa.",
            ["House.NotFound"] = "La casa non esiste o è stata rimossa.",
            ["House.RoomNameAlreadyExists"] = "Esiste già una stanza con questo nome.",
            ["Tenant.NameRequired"] = "Inserisci il nome del coinquilino.",
            ["Tenant.NotFound"] = "Il coinquilino selezionato non fa parte di questa casa.",
            ["Room.NameRequired"] = "Inserisci un nome per la stanza.",
            ["Room.NotFound"] = "La stanza selezionata non fa parte di questa casa.",
            ["CleaningTask.DescriptionRequired"] = "La descrizione dell'attività non può essere vuota.",
            ["Report.DescriptionRequired"] = "Descrivi il problema da segnalare.",
            ["Shift.InvalidDateRange"] = "La data di fine turno deve essere successiva a quella di inizio.",
            ["Shift.SelfReportNotAllowed"] = "Non puoi segnalare un tuo turno.",
            ["ShiftChecklistItem.NotFound"] = "L'attività del turno non esiste più.",
            ["General.BadRequest"] = "La richiesta non è valida. Controlla i dati inseriti.",
            ["General.Unexpected"] = "Si è verificato un errore imprevisto sul server. Riprova tra poco.",
            [ApiError.NetworkUnavailableCode] = "Impossibile contattare il server. Controlla la connessione e riprova.",
        };

        public static string GetMessage(string? errorCode, int? status)
        {
            if (errorCode is not null && MessagesByCode.TryGetValue(errorCode, out var message))
            {
                return message;
            }

            return status switch
            {
                400 => "Dati non validi. Controlla i campi inseriti.",
                404 => "Elemento non trovato.",
                409 => "L'operazione è in conflitto con dati già esistenti.",
                422 => "Operazione non consentita.",
                >= 500 and < 600 => "Il server non è al momento disponibile. Riprova tra poco.",
                _ => DefaultMessage
            };
        }
    }
}
