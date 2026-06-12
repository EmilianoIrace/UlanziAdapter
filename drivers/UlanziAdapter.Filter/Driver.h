#pragma once

#include <ntddk.h>
#include <wdf.h>
#include <hidport.h>
#include "Public.h"

typedef struct _DEVICE_CONTEXT {
    WDFQUEUE DefaultQueue;
    WDFWAITLOCK RuleLock;
    ULONG RuleCount;
    UAF_REPORT_RULE Rules[UAF_MAX_RULES];
    ULONG MatchedReports;
    ULONG RewrittenReports;
    ULONG SuppressedReports;
} DEVICE_CONTEXT, *PDEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, DeviceGetContext)

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD UafEvtDeviceAdd;
EVT_WDF_IO_QUEUE_IO_INTERNAL_DEVICE_CONTROL UafEvtIoInternalDeviceControl;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL UafEvtIoDeviceControl;
EVT_WDF_REQUEST_COMPLETION_ROUTINE UafReadReportCompletion;

NTSTATUS UafCreateDefaultQueue(_In_ WDFDEVICE Device);
VOID UafForwardRequest(_In_ WDFQUEUE Queue, _In_ WDFREQUEST Request);
VOID UafApplyRulesToReport(_In_ PDEVICE_CONTEXT Context, _Inout_updates_bytes_(ReportLength) PUCHAR Report, _In_ size_t ReportLength);
