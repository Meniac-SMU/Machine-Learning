"""Read-only Windows memory telemetry for the PS experiment."""
import ctypes
import json

class Memory(ctypes.Structure):
    _fields_=[('length',ctypes.c_ulong),('load',ctypes.c_ulong)]+[(n,ctypes.c_ulonglong) for n in
        ['total','available','page','availablePage','virtual','availableVirtual','extended']]

if __name__=='__main__':
    m=Memory();m.length=ctypes.sizeof(Memory)
    assert ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
    print(json.dumps(dict(loadPercent=m.load,availableGiB=m.available/2**30,totalGiB=m.total/2**30)))
