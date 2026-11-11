import hashlib, pyppmd, sys
WORDS = ["the","lord","of","Calradia","remembers","a","debt","owed","déjà","vu","Ærin","rode","to","Pravend","and","spoke","with","her","sister","about","the","war",",",".","\n","gold","denars","400","5","oath","sworn","Þór","naïve","château","!"]
def lcg_text(n, seed):
    x = seed; out = []; size = 0
    while size < n:
        x = (x * 1103515245 + 12345) % 2147483648
        w = WORDS[(x >> 16) % len(WORDS)]
        b = (w + " ").encode("utf-8"); out.append(b); size += len(b)
    return b"".join(out)[:n]
def enc(data, order, mem):
    e = pyppmd.Ppmd7Encoder(order, mem)
    return e.encode(data) + e.flush()
cases = [("empty", b"", 6, 1<<20), ("one", b"a", 6, 1<<20), ("abra", b"abracadabra"*50, 4, 1<<20),
         ("lcg_o2", lcg_text(50000, 1), 2, 1<<20), ("lcg_o8", lcg_text(200000, 2), 8, 16<<20),
         ("lcg_o64", lcg_text(100000, 3), 64, 16<<20), ("lcg_tiny_mem", lcg_text(300000, 4), 16, 1<<16),
         ("lcg_small_mem", lcg_text(400000, 5), 8, 1<<18), ("bytes_all", bytes(range(256))*40, 6, 1<<20)]
for name, data, order, mem in cases:
    c = enc(data, order, mem)
    d = pyppmd.Ppmd7Decoder(order, mem); back = d.decode(c, len(data))
    assert back == data, name
    print(f'("{name}", {len(data)}, {order}, {mem}, {len(c)}, "{hashlib.sha256(c).hexdigest()}"),')
