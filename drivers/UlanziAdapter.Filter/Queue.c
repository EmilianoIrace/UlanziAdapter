#include "Driver.h"

NTSTATUS
UafCreateDefaultQueue(
    _In_ WDFDEVICE Device
    )
{
    WDF_IO_QUEUE_CONFIG queueConfig;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchParallel);
    queueConfig.EvtIoInternalDeviceControl = UafEvtIoInternalDeviceControl;
    queueConfig.EvtIoDeviceControl = UafEvtIoDeviceControl;

    WDFQUEUE queue;
    NTSTATUS status = WdfIoQueueCreate(Device, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, &queue);
    if (NT_SUCCESS(status)) {
        DeviceGetContext(Device)->DefaultQueue = queue;
    }

    return status;
}

VOID
UafEvtIoInternalDeviceControl(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t OutputBufferLength,
    _In_ size_t InputBufferLength,
    _In_ ULONG IoControlCode
    )
{
    UNREFERENCED_PARAMETER(OutputBufferLength);
    UNREFERENCED_PARAMETER(InputBufferLength);

    if (IoControlCode == IOCTL_HID_READ_REPORT) {
        WdfRequestFormatRequestUsingCurrentType(Request);
        WdfRequestSetCompletionRoutine(Request, UafReadReportCompletion, WdfIoQueueGetDevice(Queue));

        if (WdfRequestSend(Request, WdfDeviceGetIoTarget(WdfIoQueueGetDevice(Queue)), WDF_NO_SEND_OPTIONS)) {
            return;
        }

        WdfRequestComplete(Request, WdfRequestGetStatus(Request));
        return;
    }

    UafForwardRequest(Queue, Request);
}

VOID
UafEvtIoDeviceControl(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t OutputBufferLength,
    _In_ size_t InputBufferLength,
    _In_ ULONG IoControlCode
    )
{
    UNREFERENCED_PARAMETER(OutputBufferLength);
    UNREFERENCED_PARAMETER(InputBufferLength);

    WDFDEVICE device = WdfIoQueueGetDevice(Queue);
    PDEVICE_CONTEXT context = DeviceGetContext(device);
    NTSTATUS status = STATUS_SUCCESS;
    size_t bytesReturned = 0;

    switch (IoControlCode) {
    case IOCTL_UAF_CLEAR_RULES:
        WdfWaitLockAcquire(context->RuleLock, NULL);
        context->RuleCount = 0;
        RtlZeroMemory(context->Rules, sizeof(context->Rules));
        context->MatchedReports = 0;
        context->RewrittenReports = 0;
        context->SuppressedReports = 0;
        WdfWaitLockRelease(context->RuleLock);
        break;

    case IOCTL_UAF_ADD_RULE:
    {
        PUAF_REPORT_RULE inputRule;
        status = WdfRequestRetrieveInputBuffer(Request, sizeof(UAF_REPORT_RULE), (PVOID*)&inputRule, NULL);
        if (!NT_SUCCESS(status)) {
            break;
        }

        if (inputRule->MatchLength == 0 ||
            inputRule->MatchLength > UAF_MAX_REPORT_BYTES ||
            inputRule->ReplacementLength > UAF_MAX_REPORT_BYTES) {
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        WdfWaitLockAcquire(context->RuleLock, NULL);
        if (context->RuleCount >= UAF_MAX_RULES) {
            status = STATUS_INSUFFICIENT_RESOURCES;
        } else {
            context->Rules[context->RuleCount++] = *inputRule;
        }
        WdfWaitLockRelease(context->RuleLock);
        break;
    }

    case IOCTL_UAF_GET_STATUS:
    {
        PUAF_STATUS outputStatus;
        status = WdfRequestRetrieveOutputBuffer(Request, sizeof(UAF_STATUS), (PVOID*)&outputStatus, NULL);
        if (!NT_SUCCESS(status)) {
            break;
        }

        WdfWaitLockAcquire(context->RuleLock, NULL);
        outputStatus->RuleCount = context->RuleCount;
        outputStatus->MatchedReports = context->MatchedReports;
        outputStatus->RewrittenReports = context->RewrittenReports;
        outputStatus->SuppressedReports = context->SuppressedReports;
        WdfWaitLockRelease(context->RuleLock);
        bytesReturned = sizeof(UAF_STATUS);
        break;
    }

    default:
        status = STATUS_INVALID_DEVICE_REQUEST;
        break;
    }

    WdfRequestCompleteWithInformation(Request, status, bytesReturned);
}

VOID
UafForwardRequest(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request
    )
{
    WdfRequestFormatRequestUsingCurrentType(Request);

    if (WdfRequestSend(Request, WdfDeviceGetIoTarget(WdfIoQueueGetDevice(Queue)), WDF_NO_SEND_OPTIONS)) {
        return;
    }

    WdfRequestComplete(Request, WdfRequestGetStatus(Request));
}

VOID
UafReadReportCompletion(
    _In_ WDFREQUEST Request,
    _In_ WDFIOTARGET Target,
    _In_ PWDF_REQUEST_COMPLETION_PARAMS Params,
    _In_ WDFCONTEXT Context
    )
{
    UNREFERENCED_PARAMETER(Target);

    if (NT_SUCCESS(Params->IoStatus.Status)) {
        PUCHAR report;
        size_t reportLength;
        NTSTATUS status = WdfRequestRetrieveOutputBuffer(Request, 1, (PVOID*)&report, &reportLength);
        if (NT_SUCCESS(status)) {
            UafApplyRulesToReport(DeviceGetContext((WDFDEVICE)Context), report, reportLength);
        }
    }

    WdfRequestComplete(Request, Params->IoStatus.Status);
}
