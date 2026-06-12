#pragma once

#include <winioctl.h>

#define UAF_DOS_DEVICE_NAME L"\\DosDevices\\UlanziAdapterFilter"
#define UAF_WIN32_DEVICE_NAME L"\\\\.\\UlanziAdapterFilter"

#define UAF_MAX_REPORT_BYTES 64
#define UAF_MAX_RULE_NAME 64
#define UAF_MAX_RULES 64

#define UAF_RULE_FLAG_SUPPRESS 0x00000001

#define IOCTL_UAF_CLEAR_RULES \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_ANY_ACCESS)

#define IOCTL_UAF_ADD_RULE \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_ANY_ACCESS)

#define IOCTL_UAF_GET_STATUS \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_BUFFERED, FILE_ANY_ACCESS)

typedef struct _UAF_REPORT_RULE {
    ULONG Flags;
    ULONG MatchLength;
    UCHAR Match[UAF_MAX_REPORT_BYTES];
    ULONG ReplacementLength;
    UCHAR Replacement[UAF_MAX_REPORT_BYTES];
    CHAR Name[UAF_MAX_RULE_NAME];
} UAF_REPORT_RULE, *PUAF_REPORT_RULE;

typedef struct _UAF_STATUS {
    ULONG RuleCount;
    ULONG MatchedReports;
    ULONG RewrittenReports;
    ULONG SuppressedReports;
} UAF_STATUS, *PUAF_STATUS;
