package main

/*
#include <stdlib.h>
*/
import "C"

import "unsafe"

//export KindExecuteJSON
func KindExecuteJSON(input *C.char) *C.char {
	return C.CString(string(handleJSON([]byte(C.GoString(input)))))
}

//export KindFree
func KindFree(value unsafe.Pointer) {
	C.free(value)
}
