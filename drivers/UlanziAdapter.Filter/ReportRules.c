#include "Driver.h"

VOID
UafApplyRulesToReport(
    _In_ PDEVICE_CONTEXT Context,
    _Inout_updates_bytes_(ReportLength) PUCHAR Report,
    _In_ size_t ReportLength
    )
{
    WdfWaitLockAcquire(Context->RuleLock, NULL);

    for (ULONG i = 0; i < Context->RuleCount; i++) {
        PUAF_REPORT_RULE rule = &Context->Rules[i];
        if (rule->MatchLength > ReportLength) {
            continue;
        }

        if (RtlCompareMemory(Report, rule->Match, rule->MatchLength) != rule->MatchLength) {
            continue;
        }

        Context->MatchedReports++;

        if ((rule->Flags & UAF_RULE_FLAG_SUPPRESS) != 0) {
            RtlZeroMemory(Report, ReportLength);
            Context->SuppressedReports++;
            break;
        }

        if (rule->ReplacementLength > 0) {
            ULONG bytesToCopy = (ULONG)min((size_t)rule->ReplacementLength, ReportLength);
            RtlZeroMemory(Report, ReportLength);
            RtlCopyMemory(Report, rule->Replacement, bytesToCopy);
            Context->RewrittenReports++;
            break;
        }
    }

    WdfWaitLockRelease(Context->RuleLock);
}
