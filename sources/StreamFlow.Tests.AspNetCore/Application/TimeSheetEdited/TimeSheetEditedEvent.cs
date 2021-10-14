using System;

namespace StreamFlow.Tests.AspNetCore.Application.TimeSheetEdited
{
    public class TimeSheetEditedEvent
    {
        public Guid PortfolioId { get; set; }
        public Guid SecurityUserId { get; set; }
        public Guid TimeEntryId { get; set; }
        public TimeSheetChangesDto Changes { get; set; }
    }

    public class StateChangeObj<T>
    {
        public T Old { get; set; }
        public T New { get; set; }

        public StateChangeObj(T old, T @new)
        {
            Old = old;
            New = @new;
        }
    }

    public class TimeSheetChangesDto
    {
        public StateChangeObj<Guid?> ClientId { get; set; }
        public StateChangeObj<Guid?> CaseId { get; set; }
        public StateChangeObj<Guid?> ActivityId { get; set; }
        public StateChangeObj<int> UserTimeInMinutes { get; set; }
        public StateChangeObj<int> PayableTimeInMinutes { get; set; }
        public StateChangeObj<decimal> TotalAmount { get; set; }
        public StateChangeObj<DateTime?> DeleteDate { get; set; }
        public StateChangeObj<DateTime> EntryDate { get; set; }
    }
}
