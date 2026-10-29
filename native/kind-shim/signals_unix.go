//go:build linux || darwin

package main

/*
#include <signal.h>

// .NET installs its child-process handler lazily. If SIGCHLD still has its
// default disposition, leave a no-op handler so a later .NET registration
// inherits SA_ONSTACK rather than running on a small Go goroutine stack.
// Do not replace an existing host handler or change ignored-child semantics.
static void kind_child_signal(int signal) { (void)signal; }

static int kind_prepare_sigchld(void) {
    struct sigaction action;
    if (sigaction(SIGCHLD, NULL, &action) != 0) return -1;
    if (action.sa_handler == SIG_IGN) return 0;
    if (action.sa_handler == SIG_DFL) {
        action.sa_handler = kind_child_signal;
        action.sa_flags &= ~SA_SIGINFO;
    }
    action.sa_flags |= SA_ONSTACK;
    return sigaction(SIGCHLD, &action, NULL);
}
*/
import "C"

import "fmt"

func prepareSignalHandlers() error {
	if result, err := C.kind_prepare_sigchld(); result != 0 {
		return fmt.Errorf("prepare Kind child-process signal handler: %w", err)
	}
	return nil
}
