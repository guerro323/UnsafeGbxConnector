using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Text;
using NLog;
using U8Xml;
using UnsafeGbxConnector.Serialization;
using UnsafeGbxConnector.Serialization.Readers;

namespace UnsafeGbxConnector
{
    public partial class GbxConnection
    {
        private bool ReceiveCore()
        {
            try
            {
                var size = _reader.ReadUInt32();
                var handle = _reader.ReadUInt32();
                if (size == 0 || handle == 0)
                {
                    Logger.Warn("Received a default handle and default size");
                    return true;
                }

                var bytesPool = ArrayPool<byte>.Shared;
                var charsPool = ArrayPool<char>.Shared;

                var byteArray = bytesPool.Rent((int) size);
                
                var count = (int) size;
                var numRead = 0;
                // copy from byte[] BinaryReader.Read(count);
                do
                {
                    var n = _reader.Read(byteArray, numRead, count);
                    if (n == 0)
                    {
                        break;
                    }

                    numRead += n;
                    count -= n;
                } while (count > 0);
                
                var bytes = new Span<byte>(byteArray, 0, numRead);
                
                var charArray = charsPool.Rent(Encoding.UTF8.GetCharCount(bytes));
                var charCount = Encoding.UTF8.GetChars(bytes, charArray);

                using var xml = XmlParser.Parse(charArray.AsSpan(0, charCount));
                
                bytesPool.Return(byteArray);
                charsPool.Return(charArray);
                
                if (xml.Root.Name == "methodResponse")
                {
                    if (Logger.IsTraceEnabled)
                    {
                        Logger.Trace("Received 'methodResponse' (handle={handle}, size={size})",
                            handle,
                            size);
                    }

                    var methodResponse = xml.Root;
                    if (methodResponse.TryFindChild("fault", out var fault))
                    {
                        var currentNode = fault;
                        currentNode = currentNode.FindChild("value");
                        currentNode = currentNode.FindChild("struct");
                        
                        // Increment back callLimit
                        _queueableCallLimit += _outboundMultiCalls[handle].Length;

                        var structReader = new GbxStructReader(currentNode);
                        var faultCode = structReader["faultCode"].ReadInt();
                        var faultString = structReader["faultString"].ReadString();

                        if (Logger.IsErrorEnabled)
                        {
                            Logger.Error("Multicall error ({code}) '{string}'",
                                faultCode,
                                faultString);
                        }
                    }
                    else if (methodResponse.TryFindChild("params", out var xmlParamArray))
                    {
                        var currentNode = xmlParamArray;
                        currentNode = currentNode.FindChild("param");
                        currentNode = currentNode.FindChild("value");
                        currentNode = currentNode.FindChild("array");
                        currentNode = currentNode.FindChild("data");

                        var childrenCount = currentNode.Children.Count;
                        // Increment back callLimit
                        _queueableCallLimit += childrenCount;

                        var actionArray = _outboundMultiCalls[handle];

                        currentNode = currentNode.FindChild("value");
                        for (var i = 0; i < childrenCount; i++)
                        {
                            var child = currentNode;
                            // A struct here mean it's a fault
                            var isFault = child.TryFindChild("struct", out fault);
                            if (isFault)
                            {
                                var structReader = new GbxStructReader(fault);
                                var faultCode = structReader["faultCode"].ReadInt();
                                var faultString = structReader["faultString"].ReadString();

                                var logLevel = LogLevel.Error;
                                
                                var action = actionArray[i];
                                if (action != null)
                                {
                                    var incoming = new GbxResponse(new GbxError(faultCode, faultString));
                                    action(incoming);
                                    
                                    // the error is already being tracked, so it will go into the Trace category
                                    logLevel = LogLevel.Trace;
                                }

                                if (Logger.IsTraceEnabled)
                                {
                                    Logger.Log(logLevel, "Call error ({code}) '{string}'",
                                        faultCode,
                                        faultString);
                                }
                            }
                            else
                            {
                                child = child.FindChild("array");
                                child = child.FindChild("data");
                                child = child.FindChild("value");

                                var action = actionArray[i];
                                if (action != null)
                                {
                                    var incoming = new GbxResponse(new GbxReader(child));
                                    action(incoming);
                                }
                            }
                            
                            currentNode.NextSibling.TryGetValue(out currentNode);
                        }
                        
                        _multiThreadTcsPool.Return(actionArray, true);
                    }
                    else if (Logger.IsWarnEnabled)
                    {
                        Logger.Warn("Received a wrong xml\n{xml}", xml.AsRawString());
                    }
                }
                // Callback
                else if (xml.Root.Name == "methodCall")
                {
                    var xmlMethodName = xml.Root.FindChild("methodName");
                    var xmlParams = xml.Root.FindChild("params");

                    if (Logger.IsTraceEnabled)
                    {
                        Logger.Trace("Received callback '{methodName}'",
                            xmlMethodName.InnerText.ToString());
                    }

                    OnCallback?.Invoke(new GbxCallback
                    (
                        xmlMethodName.InnerText,
                        new GbxReader(xmlParams, true)
                    ));
                }
                else if (Logger.IsWarnEnabled)
                {
                    Logger.Warn("Unknown rpc method.\n{xml}", xml.AsRawString());
                }
            }
            catch (Exception ex)
            {
                if (ex is not IOException {InnerException: SocketException})
                    throw;

                if (Logger.IsErrorEnabled)
                {
                    Logger.Error(ex.InnerException, "IOException Received; Reconnecting.");
                }

                if (false == _ccs.IsCancellationRequested)
                    Connect(_lastEp);

                return false;

            }

            return true;
        }

        public bool ReceiveUpdates()
        {
            if (EnableReceiveThread)
                throw new InvalidOperationException("ReceiveUpdates is already being threaded");

            return ReceiveCore();
        }

        private void ReceiveLoop()
        {
            while (false == _ccs.IsCancellationRequested)
            {
                if (_reader == null || !((NetworkStream) _reader.BaseStream).CanRead)
                    continue;

                if (ReceiveCore() == false)
                    return;
            }
        }
    }
}