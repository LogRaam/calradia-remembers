// Code written by Gabriel Mailhot, 10/11/2026.
// PPMd variant H with the 7z range coder: a port of Igor Pavlov's Ppmd7.c, Ppmd7Enc.c and Ppmd7Dec.c (LZMA SDK,
// 2017-04-03, public domain), itself based on Dmitry Shkarin's PPMd var.H (2001, public domain).
//
// Gabriel (10/11/2026): "est-ce qu'on aurait un gain si on utilisait une compression LZW, ZIP, PPM ou autre de nos donnees
// avant de les placer dans une sauvegarde?", then, after the codec bench on an inflated campaign (41.5 MB of store JSON):
// "je crois que ppmd est le bon choix", and "go" for the port. PPMd was the smallest on both stores (relations 53% of the
// GZip CR shipped, memories far less), reading back in about a second for 41 MB.
//
// The model lives in one byte[] and every reference is an offset into it, as the C code does without PPMD_32BIT, so the
// structures keep their C layout byte for byte (a state is 6 bytes, a context or a free node 12) and the output is the
// same bit for bit as 7-Zip's and pyppmd's Ppmd7Encoder (PpmdTests checks it against vectors made with pyppmd). Unsigned
// arithmetic stays unsigned where the C relies on wrapping. A one-bit slip corrupts everything after it, so the port
// follows the C line by line rather than restructuring it.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Saving
{
   /// <summary>PPMd variant H (7z range coder): encodes and decodes one block whose length the caller keeps. Pure.</summary>
   public static class Ppmd7
   {
      /// <summary>The lowest model order PPMd var.H accepts.</summary>
      public const int MinOrder = 2;

      /// <summary>The highest model order PPMd var.H accepts.</summary>
      public const int MaxOrder = 64;

      /// <summary>The smallest model memory PPMd var.H accepts, in bytes.</summary>
      public const int MinMemory = 1 << 11;

      /// <summary>The bytes compressed with a model of this order and memory; no end mark (the caller keeps the length).</summary>
      public static byte[] Encode(byte[] data, int order, int memory)
      {
         Check(order, memory);
         var model = new Model((uint) memory, (uint) order);
         var rc = new RangeEncoder(data == null ? 16 : data.Length / 3 + 16);
         if (data != null)
            foreach (byte b in data)
               model.EncodeSymbol(rc, b);

         rc.Flush();

         return rc.ToArray();
      }

      /// <summary>
      ///   The <paramref name="length" /> bytes <paramref name="packed" /> holds, read with the order and memory it was written
      ///   with. Throws on a damaged stream.
      /// </summary>
      public static byte[] Decode(byte[] packed, int length, int order, int memory)
      {
         Check(order, memory);
         if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

         var model = new Model((uint) memory, (uint) order);
         var rc = new RangeDecoder(packed ?? new byte[0]);
         if (!rc.Init()) throw new InvalidOperationException("PPMd stream does not open as one.");

         var output = new byte[length];
         for (var i = 0; i < length; i++)
         {
            int symbol = model.DecodeSymbol(rc);
            if (symbol < 0) throw new InvalidOperationException($"PPMd stream ended or broke at byte {i} of {length}.");

            output[i] = (byte) symbol;
         }

         return output;
      }

      private static void Check(int order, int memory)
      {
         if (order < MinOrder || order > MaxOrder) throw new ArgumentOutOfRangeException(nameof(order));
         if (memory < MinMemory) throw new ArgumentOutOfRangeException(nameof(memory));
      }

      #region range coder

      private sealed class RangeEncoder
      {
         private const uint TopValue = 1 << 24;
         private byte[] _out;
         private int _count;
         private ulong _low;
         private uint _range = 0xFFFFFFFF;
         private byte _cache;
         private ulong _cacheSize = 1;

         public RangeEncoder(int capacity) => _out = new byte[Math.Max(16, capacity)];

         public byte[] ToArray()
         {
            var result = new byte[_count];
            Buffer.BlockCopy(_out, 0, result, 0, _count);

            return result;
         }

         private void Write(byte b)
         {
            if (_count == _out.Length) Array.Resize(ref _out, _out.Length * 2);
            _out[_count++] = b;
         }

         private void ShiftLow()
         {
            if ((uint) _low < 0xFF000000u || (uint) (_low >> 32) != 0)
            {
               byte temp = _cache;
               do
               {
                  Write((byte) (temp + (byte) (_low >> 32)));
                  temp = 0xFF;
               }
               while (--_cacheSize != 0);
               _cache = (byte) ((uint) _low >> 24);
            }

            _cacheSize++;
            _low = (uint) _low << 8;
         }

         public void Encode(uint start, uint size, uint total)
         {
            _low += start * (ulong) (_range /= total);
            _range *= size;
            while (_range < TopValue)
            {
               _range <<= 8;
               ShiftLow();
            }
         }

         public void EncodeBit0(uint size0)
         {
            _range = (_range >> 14) * size0;
            while (_range < TopValue)
            {
               _range <<= 8;
               ShiftLow();
            }
         }

         public void EncodeBit1(uint size0)
         {
            uint newBound = (_range >> 14) * size0;
            _low += newBound;
            _range -= newBound;
            while (_range < TopValue)
            {
               _range <<= 8;
               ShiftLow();
            }
         }

         public void Flush()
         {
            for (var i = 0; i < 5; i++) ShiftLow();
         }
      }

      private sealed class RangeDecoder
      {
         private const uint TopValue = 1 << 24;
         private readonly byte[] _in;
         private int _pos;
         private uint _range;
         private uint _code;

         public RangeDecoder(byte[] input) => _in = input;

         private uint Read() => _pos < _in.Length ? _in[_pos++] : 0u;

         public bool Init()
         {
            _code = 0;
            _range = 0xFFFFFFFF;
            if (Read() != 0) return false;

            for (var i = 0; i < 4; i++) _code = (_code << 8) | Read();

            return _code < 0xFFFFFFFF;
         }

         public uint GetThreshold(uint total) => _code / (_range /= total);

         private void Normalize()
         {
            if (_range < TopValue)
            {
               _code = (_code << 8) | Read();
               _range <<= 8;
               if (_range < TopValue)
               {
                  _code = (_code << 8) | Read();
                  _range <<= 8;
               }
            }
         }

         public void Decode(uint start, uint size)
         {
            _code -= start * _range;
            _range *= size;
            Normalize();
         }

         public uint DecodeBit(uint size0)
         {
            uint newBound = (_range >> 14) * size0;
            uint symbol;
            if (_code < newBound)
            {
               symbol = 0;
               _range = newBound;
            }
            else
            {
               symbol = 1;
               _code -= newBound;
               _range -= newBound;
            }

            Normalize();

            return symbol;
         }
      }

      #endregion

      #region model

      // Layout, as in the C structures (little-endian, packed):
      //   state   (6):  Symbol @0, Freq @1, Successor @2 (low 16 bits) and @4 (high 16 bits), read here as one uint @2
      //   context (12): NumStats @0, SummFreq @2, Stats @4, Suffix @8; a one-state context keeps its state at @2
      //   node    (12): Stamp @0, NU @2, Next @4, Prev @8; a free-list link is the uint @0
      private sealed class Model
      {
         private const int IntBits = 7;
         private const int PeriodBits = 7;
         private const int BinScale = 1 << (IntBits + PeriodBits);
         private const int NumIndexes = 4 + 4 + 4 + (128 + 3 - 1 * 4 - 2 * 4 - 3 * 4) / 4;
         private const uint MaxFreq = 124;
         private const uint UnitSize = 12;
         private const int Dummy = 25 * 16;

         private static readonly byte[] ExpEscape = {25, 14, 9, 7, 5, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2};
         private static readonly ushort[] InitBinEsc = {0x3CDD, 0x1F3F, 0x59BF, 0x48F3, 0x64A1, 0x5ABC, 0x6632, 0x6051};

         private readonly byte[] _m;
         private readonly uint _size;
         private readonly uint _alignOffset;

         private uint _minContext, _maxContext, _foundState;
         private uint _orderFall, _initEsc, _prevSuccess, _maxOrder, _hiBitsFlag;
         private int _runLength, _initRL;
         private uint _glueCount;
         private uint _loUnit, _hiUnit, _text, _unitsStart;

         private readonly byte[] _indx2Units = new byte[NumIndexes];
         private readonly byte[] _units2Indx = new byte[128];
         private readonly uint[] _freeList = new uint[NumIndexes];
         private readonly byte[] _ns2Indx = new byte[256];
         private readonly byte[] _ns2BSIndx = new byte[256];
         private readonly byte[] _hb2Flag = new byte[256];
         private readonly ushort[] _seeSumm = new ushort[25 * 16 + 1];
         private readonly byte[] _seeShift = new byte[25 * 16 + 1];
         private readonly byte[] _seeCount = new byte[25 * 16 + 1];
         private readonly ushort[] _binSumm = new ushort[128 * 64];

         private readonly uint[] _ps = new uint[MaxOrder];
         private readonly uint[] _decodePs = new uint[256];
         private readonly sbyte[] _charMask = new sbyte[256];
         private readonly byte[] _tmp = new byte[6];

         public Model(uint size, uint maxOrder)
         {
            // Ppmd7_Construct
            uint i, k, m;
            for (i = 0, k = 0; i < NumIndexes; i++)
            {
               uint step = i >= 12 ? 4 : (i >> 2) + 1;
               do { _units2Indx[k++] = (byte) i; } while (--step != 0);
               _indx2Units[i] = (byte) k;
            }

            _ns2BSIndx[0] = 0 << 1;
            _ns2BSIndx[1] = 1 << 1;
            for (i = 2; i < 11; i++) _ns2BSIndx[i] = 2 << 1;
            for (i = 11; i < 256; i++) _ns2BSIndx[i] = 3 << 1;

            for (i = 0; i < 3; i++) _ns2Indx[i] = (byte) i;
            for (m = i, k = 1; i < 256; i++)
            {
               _ns2Indx[i] = (byte) m;
               if (--k == 0) k = ++m - 2;
            }

            for (i = 0; i < 0x40; i++) _hb2Flag[i] = 0;
            for (i = 0x40; i < 0x100; i++) _hb2Flag[i] = 8;

            // Ppmd7_Alloc: the model's memory, plus one unit past it for GlueFreeBlocks' list head.
            _size = size;
            _alignOffset = 4 - (size & 3);
            _m = new byte[_alignOffset + size + UnitSize];

            // Ppmd7_Init
            _maxOrder = maxOrder;
            RestartModel();
            _seeShift[Dummy] = PeriodBits;
            _seeSumm[Dummy] = 0;
            _seeCount[Dummy] = 64;
         }

         #region memory

         private ushort U16(uint o) => (ushort) (_m[o] | (_m[o + 1] << 8));

         private void SetU16(uint o, uint v)
         {
            _m[o] = (byte) v;
            _m[o + 1] = (byte) (v >> 8);
         }

         private uint U32(uint o) => (uint) (_m[o] | (_m[o + 1] << 8) | (_m[o + 2] << 16) | (_m[o + 3] << 24));

         private void SetU32(uint o, uint v)
         {
            _m[o] = (byte) v;
            _m[o + 1] = (byte) (v >> 8);
            _m[o + 2] = (byte) (v >> 16);
            _m[o + 3] = (byte) (v >> 24);
         }

         private byte Sym(uint s) => _m[s];
         private byte Freq(uint s) => _m[s + 1];
         private void SetFreq(uint s, uint v) => _m[s + 1] = (byte) v;
         private uint Successor(uint s) => U32(s + 2);
         private void SetSuccessor(uint s, uint v) => SetU32(s + 2, v);

         private uint NumStats(uint c) => U16(c);
         private void SetNumStats(uint c, uint v) => SetU16(c, v);
         private uint SummFreq(uint c) => U16(c + 2);
         private void SetSummFreq(uint c, uint v) => SetU16(c + 2, v);
         private uint Stats(uint c) => U32(c + 4);
         private void SetStats(uint c, uint v) => SetU32(c + 4, v);
         private uint Suffix(uint c) => U32(c + 8);
         private void SetSuffix(uint c, uint v) => SetU32(c + 8, v);
         private static uint OneState(uint c) => c + 2;

         private uint Stamp(uint n) => U16(n);
         private void SetStamp(uint n, uint v) => SetU16(n, v);
         private uint NU(uint n) => U16(n + 2);
         private void SetNU(uint n, uint v) => SetU16(n + 2, v);
         private uint Next(uint n) => U32(n + 4);
         private void SetNext(uint n, uint v) => SetU32(n + 4, v);
         private uint Prev(uint n) => U32(n + 8);
         private void SetPrev(uint n, uint v) => SetU32(n + 8, v);

         private void CopyState(uint dest, uint src) => Buffer.BlockCopy(_m, (int) src, _m, (int) dest, 6);

         private void SwapStates(uint t1, uint t2)
         {
            for (uint i = 0; i < 6; i++)
            {
               byte t = _m[t1 + i];
               _m[t1 + i] = _m[t2 + i];
               _m[t2 + i] = t;
            }
         }

         private static uint U2B(uint nu) => nu * UnitSize;
         private uint U2I(uint nu) => _units2Indx[nu - 1];
         private uint I2U(uint indx) => _indx2Units[indx];

         private void InsertNode(uint node, uint indx)
         {
            SetU32(node, _freeList[indx]);
            _freeList[indx] = node;
         }

         private uint RemoveNode(uint indx)
         {
            uint node = _freeList[indx];
            _freeList[indx] = U32(node);

            return node;
         }

         private void SplitBlock(uint ptr, uint oldIndx, uint newIndx)
         {
            uint i, nu = I2U(oldIndx) - I2U(newIndx);
            ptr += U2B(I2U(newIndx));
            if (I2U(i = U2I(nu)) != nu)
            {
               uint k = I2U(--i);
               InsertNode(ptr + U2B(k), nu - k - 1);
            }

            InsertNode(ptr, i);
         }

         private void GlueFreeBlocks()
         {
            uint head = _alignOffset + _size;
            uint n = head;
            uint i;

            _glueCount = 255;

            // the doubly-linked list of free blocks
            for (i = 0; i < NumIndexes; i++)
            {
               uint nu = I2U(i);
               uint next = _freeList[i];
               _freeList[i] = 0;
               while (next != 0)
               {
                  uint node = next;
                  SetNext(node, n);
                  SetPrev(n, next);
                  n = next;
                  next = U32(node);
                  SetStamp(node, 0);
                  SetNU(node, nu);
               }
            }

            SetStamp(head, 1);
            SetNext(head, n);
            SetPrev(n, head);
            if (_loUnit != _hiUnit) SetStamp(_loUnit, 1);

            // glue free blocks
            while (n != head)
            {
               uint node = n;
               uint nu = NU(node);
               for (;;)
               {
                  uint node2 = node + nu * UnitSize;
                  nu += NU(node2);
                  if (Stamp(node2) != 0 || nu >= 0x10000) break;

                  SetNext(Prev(node2), Next(node2));
                  SetPrev(Next(node2), Prev(node2));
                  SetNU(node, nu);
               }

               n = Next(node);
            }

            // fill the lists of free blocks
            for (n = Next(head); n != head;)
            {
               uint node = n;
               uint nu;
               uint next = Next(node);
               for (nu = NU(node); nu > 128; nu -= 128, node += 128 * UnitSize) InsertNode(node, NumIndexes - 1);
               if (I2U(i = U2I(nu)) != nu)
               {
                  uint k = I2U(--i);
                  InsertNode(node + k * UnitSize, nu - k - 1);
               }

               InsertNode(node, i);
               n = next;
            }
         }

         private uint AllocUnitsRare(uint indx)
         {
            uint i;
            if (_glueCount == 0)
            {
               GlueFreeBlocks();
               if (_freeList[indx] != 0) return RemoveNode(indx);
            }

            i = indx;
            do
            {
               if (++i == NumIndexes)
               {
                  uint numBytes = U2B(I2U(indx));
                  _glueCount--;

                  return _unitsStart - _text > numBytes ? _unitsStart -= numBytes : 0;
               }
            }
            while (_freeList[i] == 0);

            uint retVal = RemoveNode(i);
            SplitBlock(retVal, i, indx);

            return retVal;
         }

         private uint AllocUnits(uint indx)
         {
            if (_freeList[indx] != 0) return RemoveNode(indx);

            uint numBytes = U2B(I2U(indx));
            if (numBytes <= _hiUnit - _loUnit)
            {
               uint retVal = _loUnit;
               _loUnit += numBytes;

               return retVal;
            }

            return AllocUnitsRare(indx);
         }

         private void Copy12(uint dest, uint src, uint num) => Buffer.BlockCopy(_m, (int) src, _m, (int) dest, (int) (num * UnitSize));

         private uint ShrinkUnits(uint oldPtr, uint oldNU, uint newNU)
         {
            uint i0 = U2I(oldNU);
            uint i1 = U2I(newNU);
            if (i0 == i1) return oldPtr;

            if (_freeList[i1] != 0)
            {
               uint ptr = RemoveNode(i1);
               Copy12(ptr, oldPtr, newNU);
               InsertNode(oldPtr, i0);

               return ptr;
            }

            SplitBlock(oldPtr, i0, i1);

            return oldPtr;
         }

         #endregion

         private void RestartModel()
         {
            uint i, k, m;

            Array.Clear(_freeList, 0, _freeList.Length);
            _text = _alignOffset;
            _hiUnit = _text + _size;
            _loUnit = _unitsStart = _hiUnit - _size / 8 / UnitSize * 7 * UnitSize;
            _glueCount = 0;

            _orderFall = _maxOrder;
            _runLength = _initRL = -(int) (_maxOrder < 12 ? _maxOrder : 12) - 1;
            _prevSuccess = 0;

            _minContext = _maxContext = _hiUnit -= UnitSize;
            SetSuffix(_minContext, 0);
            SetNumStats(_minContext, 256);
            SetSummFreq(_minContext, 256 + 1);
            _foundState = _loUnit;
            _loUnit += U2B(256 / 2);
            SetStats(_minContext, _foundState);
            for (i = 0; i < 256; i++)
            {
               uint s = _foundState + i * 6;
               _m[s] = (byte) i;
               _m[s + 1] = 1;
               SetSuccessor(s, 0);
            }

            for (i = 0; i < 128; i++)
               for (k = 0; k < 8; k++)
               {
                  var val = (ushort) (BinScale - InitBinEsc[k] / (i + 2));
                  for (m = 0; m < 64; m += 8) _binSumm[i * 64 + k + m] = val;
               }

            for (i = 0; i < 25; i++)
               for (k = 0; k < 16; k++)
               {
                  uint see = i * 16 + k;
                  _seeShift[see] = PeriodBits - 4;
                  _seeSumm[see] = (ushort) ((5 * i + 10) << _seeShift[see]);
                  _seeCount[see] = 4;
               }
         }

         private uint CreateSuccessors(bool skip)
         {
            uint c = _minContext;
            uint upBranch = Successor(_foundState);
            uint numPs = 0;

            if (!skip) _ps[numPs++] = _foundState;

            while (Suffix(c) != 0)
            {
               uint s;
               c = Suffix(c);
               if (NumStats(c) != 1)
               {
                  for (s = Stats(c); Sym(s) != Sym(_foundState); s += 6) { }
               }
               else
                  s = OneState(c);

               uint successor = Successor(s);
               if (successor != upBranch)
               {
                  c = successor;
                  if (numPs == 0) return c;

                  break;
               }

               _ps[numPs++] = s;
            }

            byte upSymbol = _m[upBranch];
            uint upSuccessor = upBranch + 1;
            byte upFreq;

            if (NumStats(c) == 1)
               upFreq = Freq(OneState(c));
            else
            {
               uint s;
               for (s = Stats(c); Sym(s) != upSymbol; s += 6) { }
               uint cf = (uint) Freq(s) - 1;
               uint s0 = SummFreq(c) - NumStats(c) - cf;
               upFreq = (byte) (1 + (2 * cf <= s0 ? (5 * cf > s0 ? 1u : 0u) : (2 * cf + 3 * s0 - 1) / (2 * s0)));
            }

            do
            {
               uint c1;
               if (_hiUnit != _loUnit)
                  c1 = _hiUnit -= UnitSize;
               else if (_freeList[0] != 0)
                  c1 = RemoveNode(0);
               else
               {
                  c1 = AllocUnitsRare(0);
                  if (c1 == 0) return 0;
               }

               SetNumStats(c1, 1);
               uint one = OneState(c1);
               _m[one] = upSymbol;
               _m[one + 1] = upFreq;
               SetSuccessor(one, upSuccessor);
               SetSuffix(c1, c);
               SetSuccessor(_ps[--numPs], c1);
               c = c1;
            }
            while (numPs != 0);

            return c;
         }

         private void UpdateModel()
         {
            uint successor, fSuccessor = Successor(_foundState);
            uint c;
            uint s0, ns;

            if (Freq(_foundState) < MaxFreq / 4 && Suffix(_minContext) != 0)
            {
               c = Suffix(_minContext);

               if (NumStats(c) == 1)
               {
                  uint s = OneState(c);
                  if (Freq(s) < 32) SetFreq(s, Freq(s) + 1u);
               }
               else
               {
                  uint s = Stats(c);
                  if (Sym(s) != Sym(_foundState))
                  {
                     do { s += 6; } while (Sym(s) != Sym(_foundState));
                     if (Freq(s) >= Freq(s - 6))
                     {
                        SwapStates(s, s - 6);
                        s -= 6;
                     }
                  }

                  if (Freq(s) < MaxFreq - 9)
                  {
                     SetFreq(s, Freq(s) + 2u);
                     SetSummFreq(c, SummFreq(c) + 2);
                  }
               }
            }

            if (_orderFall == 0)
            {
               _minContext = _maxContext = CreateSuccessors(true);
               if (_minContext == 0)
               {
                  RestartModel();
                  return;
               }

               SetSuccessor(_foundState, _minContext);
               return;
            }

            _m[_text++] = Sym(_foundState);
            successor = _text;
            if (_text >= _unitsStart)
            {
               RestartModel();
               return;
            }

            if (fSuccessor != 0)
            {
               if (fSuccessor <= successor)
               {
                  uint cs = CreateSuccessors(false);
                  if (cs == 0)
                  {
                     RestartModel();
                     return;
                  }

                  fSuccessor = cs;
               }

               if (--_orderFall == 0)
               {
                  successor = fSuccessor;
                  if (_maxContext != _minContext) _text--;
               }
            }
            else
            {
               SetSuccessor(_foundState, successor);
               fSuccessor = _minContext;
            }

            ns = NumStats(_minContext);
            s0 = SummFreq(_minContext) - ns - (Freq(_foundState) - 1u);

            for (c = _maxContext; c != _minContext; c = Suffix(c))
            {
               uint ns1;
               uint cf, sf;
               if ((ns1 = NumStats(c)) != 1)
               {
                  if ((ns1 & 1) == 0)
                  {
                     // expand for one unit
                     uint oldNU = ns1 >> 1;
                     uint i = U2I(oldNU);
                     if (i != U2I(oldNU + 1))
                     {
                        uint ptr = AllocUnits(i + 1);
                        if (ptr == 0)
                        {
                           RestartModel();
                           return;
                        }

                        uint oldPtr = Stats(c);
                        Copy12(ptr, oldPtr, oldNU);
                        InsertNode(oldPtr, i);
                        SetStats(c, ptr);
                     }
                  }

                  uint summ = SummFreq(c);
                  SetSummFreq(c, summ + (2 * ns1 < ns ? 1u : 0u) + 2 * ((4 * ns1 <= ns ? 1u : 0u) & (summ <= 8 * ns1 ? 1u : 0u)));
               }
               else
               {
                  uint s = AllocUnits(0);
                  if (s == 0)
                  {
                     RestartModel();
                     return;
                  }

                  CopyState(s, OneState(c));
                  SetStats(c, s);
                  if (Freq(s) < MaxFreq / 4 - 1)
                     SetFreq(s, (uint) Freq(s) << 1);
                  else
                     SetFreq(s, MaxFreq - 4);
                  SetSummFreq(c, Freq(s) + _initEsc + (ns > 3 ? 1u : 0u));
               }

               cf = 2 * (uint) Freq(_foundState) * (SummFreq(c) + 6);
               sf = s0 + SummFreq(c);
               if (cf < 6 * sf)
               {
                  cf = 1 + (cf > sf ? 1u : 0u) + (cf >= 4 * sf ? 1u : 0u);
                  SetSummFreq(c, SummFreq(c) + 3);
               }
               else
               {
                  cf = 4 + (cf >= 9 * sf ? 1u : 0u) + (cf >= 12 * sf ? 1u : 0u) + (cf >= 15 * sf ? 1u : 0u);
                  SetSummFreq(c, SummFreq(c) + cf);
               }

               {
                  uint s = Stats(c) + ns1 * 6;
                  SetSuccessor(s, successor);
                  _m[s] = Sym(_foundState);
                  _m[s + 1] = (byte) cf;
                  SetNumStats(c, ns1 + 1);
               }
            }

            _maxContext = _minContext = fSuccessor;
         }

         private void Rescale()
         {
            uint i, adder, sumFreq, escFreq;
            uint stats = Stats(_minContext);
            uint s = _foundState;
            {
               Buffer.BlockCopy(_m, (int) s, _tmp, 0, 6);
               for (; s != stats; s -= 6) CopyState(s, s - 6);
               Buffer.BlockCopy(_tmp, 0, _m, (int) s, 6);
            }

            escFreq = SummFreq(_minContext) - Freq(s);
            SetFreq(s, Freq(s) + 4u);
            adder = _orderFall != 0 ? 1u : 0u;
            SetFreq(s, (Freq(s) + adder) >> 1);
            sumFreq = Freq(s);

            i = NumStats(_minContext) - 1;
            do
            {
               escFreq -= Freq(s += 6);
               SetFreq(s, (Freq(s) + adder) >> 1);
               sumFreq += Freq(s);
               if (Freq(s) > Freq(s - 6))
               {
                  uint s1 = s;
                  Buffer.BlockCopy(_m, (int) s1, _tmp, 0, 6);
                  do
                     CopyState(s1, s1 - 6);
                  while ((s1 -= 6) != stats && _tmp[1] > Freq(s1 - 6));
                  Buffer.BlockCopy(_tmp, 0, _m, (int) s1, 6);
               }
            }
            while (--i != 0);

            if (Freq(s) == 0)
            {
               uint numStats = NumStats(_minContext);
               uint n0, n1;
               do { i++; } while (Freq(s -= 6) == 0);
               escFreq += i;
               SetNumStats(_minContext, NumStats(_minContext) - i);
               if (NumStats(_minContext) == 1)
               {
                  Buffer.BlockCopy(_m, (int) stats, _tmp, 0, 6);
                  do
                  {
                     _tmp[1] = (byte) (_tmp[1] - (_tmp[1] >> 1));
                     escFreq >>= 1;
                  }
                  while (escFreq > 1);

                  InsertNode(stats, U2I((numStats + 1) >> 1));
                  _foundState = OneState(_minContext);
                  Buffer.BlockCopy(_tmp, 0, _m, (int) _foundState, 6);
                  return;
               }

               n0 = (numStats + 1) >> 1;
               n1 = (NumStats(_minContext) + 1) >> 1;
               if (n0 != n1) SetStats(_minContext, ShrinkUnits(stats, n0, n1));
            }

            SetSummFreq(_minContext, sumFreq + escFreq - (escFreq >> 1));
            _foundState = Stats(_minContext);
         }

         private int MakeEscFreq(uint numMasked, out uint escFreq)
         {
            int see;
            uint numStats = NumStats(_minContext);
            uint nonMasked = numStats - numMasked;
            if (numStats != 256)
            {
               see = (int) (_ns2Indx[nonMasked - 1] * 16u
                            + (nonMasked < NumStats(Suffix(_minContext)) - numStats ? 1u : 0u)
                            + 2u * (SummFreq(_minContext) < 11 * numStats ? 1u : 0u)
                            + 4u * (numMasked > nonMasked ? 1u : 0u)
                            + _hiBitsFlag);
               uint r = (uint) (_seeSumm[see] >> _seeShift[see]);
               _seeSumm[see] = (ushort) (_seeSumm[see] - r);
               escFreq = r + (r == 0 ? 1u : 0u);
            }
            else
            {
               see = Dummy;
               escFreq = 1;
            }

            return see;
         }

         private void SeeUpdate(int see)
         {
            if (_seeShift[see] < PeriodBits && --_seeCount[see] == 0)
            {
               _seeSumm[see] = (ushort) (_seeSumm[see] << 1);
               _seeCount[see] = (byte) (3 << _seeShift[see]++);
            }
         }

         private void NextContext()
         {
            uint c = Successor(_foundState);
            if (_orderFall == 0 && c > _text)
               _minContext = _maxContext = c;
            else
               UpdateModel();
         }

         private void Update1()
         {
            uint s = _foundState;
            SetFreq(s, Freq(s) + 4u);
            SetSummFreq(_minContext, SummFreq(_minContext) + 4);
            if (Freq(s) > Freq(s - 6))
            {
               SwapStates(s, s - 6);
               _foundState = s -= 6;
               if (Freq(s) > MaxFreq) Rescale();
            }

            NextContext();
         }

         private void Update1_0()
         {
            _prevSuccess = 2u * Freq(_foundState) > SummFreq(_minContext) ? 1u : 0u;
            _runLength += (int) _prevSuccess;
            SetSummFreq(_minContext, SummFreq(_minContext) + 4);
            SetFreq(_foundState, Freq(_foundState) + 4u);
            if (Freq(_foundState) > MaxFreq) Rescale();
            NextContext();
         }

         private void UpdateBin()
         {
            SetFreq(_foundState, Freq(_foundState) + (Freq(_foundState) < 128 ? 1u : 0u));
            _prevSuccess = 1;
            _runLength++;
            NextContext();
         }

         private void Update2()
         {
            SetSummFreq(_minContext, SummFreq(_minContext) + 4);
            SetFreq(_foundState, Freq(_foundState) + 4u);
            if (Freq(_foundState) > MaxFreq) Rescale();
            _runLength = _initRL;
            UpdateModel();
         }

         private int BinSummIndex()
         {
            uint one = OneState(_minContext);
            _hiBitsFlag = _hb2Flag[Sym(_foundState)];

            return (int) ((Freq(one) - 1u) * 64
                          + _prevSuccess
                          + _ns2BSIndx[NumStats(Suffix(_minContext)) - 1]
                          + _hiBitsFlag
                          + 2u * _hb2Flag[Sym(one)]
                          + (uint) ((_runLength >> 26) & 0x20));
         }

         private void MaskAll()
         {
            for (var i = 0; i < 256; i++) _charMask[i] = -1;
         }

         public void EncodeSymbol(RangeEncoder rc, int symbol)
         {
            if (NumStats(_minContext) != 1)
            {
               uint s = Stats(_minContext);
               uint sum;
               uint i;
               if (Sym(s) == symbol)
               {
                  rc.Encode(0, Freq(s), SummFreq(_minContext));
                  _foundState = s;
                  Update1_0();
                  return;
               }

               _prevSuccess = 0;
               sum = Freq(s);
               i = NumStats(_minContext) - 1;
               do
               {
                  if (Sym(s += 6) == symbol)
                  {
                     rc.Encode(sum, Freq(s), SummFreq(_minContext));
                     _foundState = s;
                     Update1();
                     return;
                  }

                  sum += Freq(s);
               }
               while (--i != 0);

               _hiBitsFlag = _hb2Flag[Sym(_foundState)];
               MaskAll();
               _charMask[Sym(s)] = 0;
               i = NumStats(_minContext) - 1;
               do { _charMask[Sym(s -= 6)] = 0; } while (--i != 0);
               rc.Encode(sum, SummFreq(_minContext) - sum, SummFreq(_minContext));
            }
            else
            {
               int prob = BinSummIndex();
               uint s = OneState(_minContext);
               if (Sym(s) == symbol)
               {
                  rc.EncodeBit0(_binSumm[prob]);
                  _binSumm[prob] = UpdateProb0(_binSumm[prob]);
                  _foundState = s;
                  UpdateBin();
                  return;
               }

               rc.EncodeBit1(_binSumm[prob]);
               _binSumm[prob] = UpdateProb1(_binSumm[prob]);
               _initEsc = ExpEscape[_binSumm[prob] >> 10];
               MaskAll();
               _charMask[Sym(s)] = 0;
               _prevSuccess = 0;
            }

            for (;;)
            {
               uint escFreq;
               uint s;
               uint sum;
               uint i, numMasked = NumStats(_minContext);
               do
               {
                  _orderFall++;
                  if (Suffix(_minContext) == 0) return; // end marker (symbol -1)

                  _minContext = Suffix(_minContext);
               }
               while (NumStats(_minContext) == numMasked);

               int see = MakeEscFreq(numMasked, out escFreq);
               s = Stats(_minContext);
               sum = 0;
               i = NumStats(_minContext);
               do
               {
                  int cur = Sym(s);
                  if (cur == symbol)
                  {
                     uint low = sum;
                     uint s1 = s;
                     do
                     {
                        sum += (uint) (Freq(s) & _charMask[Sym(s)]);
                        s += 6;
                     }
                     while (--i != 0);

                     rc.Encode(low, Freq(s1), sum + escFreq);
                     SeeUpdate(see);
                     _foundState = s1;
                     Update2();
                     return;
                  }

                  sum += (uint) (Freq(s) & _charMask[cur]);
                  _charMask[cur] = 0;
                  s += 6;
               }
               while (--i != 0);

               rc.Encode(sum, escFreq, sum + escFreq);
               _seeSumm[see] = (ushort) (_seeSumm[see] + sum + escFreq);
            }
         }

         public int DecodeSymbol(RangeDecoder rc)
         {
            if (NumStats(_minContext) != 1)
            {
               uint s = Stats(_minContext);
               uint i;
               uint count, hiCnt;
               if ((count = rc.GetThreshold(SummFreq(_minContext))) < (hiCnt = Freq(s)))
               {
                  rc.Decode(0, Freq(s));
                  _foundState = s;
                  byte symbol = Sym(s);
                  Update1_0();
                  return symbol;
               }

               _prevSuccess = 0;
               i = NumStats(_minContext) - 1;
               do
               {
                  if ((hiCnt += Freq(s += 6)) > count)
                  {
                     rc.Decode(hiCnt - Freq(s), Freq(s));
                     _foundState = s;
                     byte symbol = Sym(s);
                     Update1();
                     return symbol;
                  }
               }
               while (--i != 0);

               if (count >= SummFreq(_minContext)) return -2;

               _hiBitsFlag = _hb2Flag[Sym(_foundState)];
               rc.Decode(hiCnt, SummFreq(_minContext) - hiCnt);
               MaskAll();
               _charMask[Sym(s)] = 0;
               i = NumStats(_minContext) - 1;
               do { _charMask[Sym(s -= 6)] = 0; } while (--i != 0);
            }
            else
            {
               int prob = BinSummIndex();
               if (rc.DecodeBit(_binSumm[prob]) == 0)
               {
                  _binSumm[prob] = UpdateProb0(_binSumm[prob]);
                  byte symbol = Sym(_foundState = OneState(_minContext));
                  UpdateBin();
                  return symbol;
               }

               _binSumm[prob] = UpdateProb1(_binSumm[prob]);
               _initEsc = ExpEscape[_binSumm[prob] >> 10];
               MaskAll();
               _charMask[Sym(OneState(_minContext))] = 0;
               _prevSuccess = 0;
            }

            for (;;)
            {
               uint s;
               uint freqSum, count, hiCnt;
               uint i, num, numMasked = NumStats(_minContext);
               do
               {
                  _orderFall++;
                  if (Suffix(_minContext) == 0) return -1;

                  _minContext = Suffix(_minContext);
               }
               while (NumStats(_minContext) == numMasked);

               hiCnt = 0;
               s = Stats(_minContext);
               i = 0;
               num = NumStats(_minContext) - numMasked;
               do
               {
                  int k = _charMask[Sym(s)];
                  hiCnt += (uint) (Freq(s) & k);
                  _decodePs[i] = s;
                  s += 6;
                  i = (uint) (i - k);
               }
               while (i != num);

               int see = MakeEscFreq(numMasked, out freqSum);
               freqSum += hiCnt;
               count = rc.GetThreshold(freqSum);

               if (count < hiCnt)
               {
                  var pps = 0;
                  for (hiCnt = 0; (hiCnt += Freq(_decodePs[pps])) <= count; pps++) { }
                  s = _decodePs[pps];
                  rc.Decode(hiCnt - Freq(s), Freq(s));
                  SeeUpdate(see);
                  _foundState = s;
                  byte symbol = Sym(s);
                  Update2();
                  return symbol;
               }

               if (count >= freqSum) return -2;

               rc.Decode(hiCnt, freqSum - hiCnt);
               _seeSumm[see] = (ushort) (_seeSumm[see] + freqSum);
               do { _charMask[Sym(_decodePs[--i])] = 0; } while (i != 0);
            }
         }

         private static ushort UpdateProb0(ushort prob) => (ushort) (prob + (1 << IntBits) - GetMean(prob));
         private static ushort UpdateProb1(ushort prob) => (ushort) (prob - GetMean(prob));
         private static int GetMean(int summ) => (summ + (1 << (PeriodBits - 2))) >> PeriodBits;
      }

      #endregion
   }
}
