# Elatec.NET
 Elatec TWN4 SimpleProtocol Wrapper for .NET

Reader-Shop: https://www.elatec-shop.de/de/

DEV-Kit: https://www.elatec-rfid.com/int/twn4-dev-pack

usage example:

    public class ElatecNetProvider, IDisposable
    {
        private readonly TWN4ReaderDevice readerDevice;
        private bool _disposed;

        #region Constructor

        public ElatecNetProvider()
        {
            try
            {
                readerDevice = new TWN4ReaderDevice(PortNumber);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }
        }

        public ElatecNetProvider(int _comPort)
        {
            try
            {
                readerDevice = new TWN4ReaderDevice(_comPort);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ERROR Connect()
        {
            readerDevice.Beep(1, 50, 1000, 100);
            readerDevice.GreenLED(true);
            readerDevice.RedLED(false);

            return TWN4ReaderDevice.Connect() == true ? ERROR.NoError : ERROR.IOError;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ERROR ReadChipPublic()
        {
            try
            {
                if (readerDevice != null)
                {
                    if (!readerDevice.IsConnected)
                    {
                        Connect();
                    }

                    var tmpTag = readerDevice.GetSingleChip(true);
                    hfTag = new GenericChipModel(tmpTag.UID, (RFiDGear.DataAccessLayer.CARD_TYPE)tmpTag.CardType, tmpTag.SAK, tmpTag.RATS, tmpTag.VersionL4);
                    tmpTag = readerDevice.GetSingleChip(false);
                    lfTag = new GenericChipModel(tmpTag.UID, (RFiDGear.DataAccessLayer.CARD_TYPE)tmpTag.CardType);
                    tmpTag = readerDevice.GetSingleChip(true, true);
                    legicTag = new GenericChipModel(tmpTag.UID, (RFiDGear.DataAccessLayer.CARD_TYPE)tmpTag.CardType);
                    readerDevice.GetSingleChip(true);

                    if (
                            !(
                                string.IsNullOrWhiteSpace(hfTag?.UID) & 
                                string.IsNullOrWhiteSpace(lfTag?.UID) &
                                string.IsNullOrWhiteSpace(legicTag?.UID)
                            )
                        )
                    {
                        try
                        {
                            readerDevice.GreenLED(true);
                            readerDevice.RedLED(false);

                            GenericChip = new GenericChipModel(hfTag.UID, 
                                (CARD_TYPE)hfTag.CardType, 
                                hfTag.SAK, 
                                hfTag.RATS,
                                hfTag.VersionL4
                                );

                                if (lfTag != null && lfTag?.CardType != CARD_TYPE.NOTAG)
                                {
                                    if(GenericChip != null && GenericChip.CardType != CARD_TYPE.NOTAG)
                                    {
                                        GenericChip.Child = new GenericChipModel(lfTag.UID, lfTag.CardType);
                                    }
                                    else
                                    {
                                        GenericChip = new GenericChipModel(lfTag.UID, lfTag.CardType);
                                    }
                                }
                                else if (legicTag != null && legicTag?.CardType != CARD_TYPE.NOTAG)
                                {
                                    if (GenericChip != null && GenericChip.CardType != CARD_TYPE.NOTAG)
                                    {
                                        GenericChip.Child = new GenericChipModel(legicTag.UID, legicTag.CardType);
                                    }
                                    else
                                    {
                                        GenericChip = new GenericChipModel(legicTag.UID, legicTag.CardType);
                                    }
                                }
                            //readerDevice.GetSingleChip(true);

                            return ERROR.NoError;
                        }
                        catch (Exception e)
                        {
                            LogWriter.CreateLogEntry(e, FacilityName);
                            return ERROR.IOError;
                        }
                    }
                    else
                    {
                        readerDevice.Beep(3, 25, 600, 100);
                        readerDevice.RedLED(true);
                        GenericChip = null;

                        return ERROR.NotReadyError;
                    }
                }

                else
                {
                    return ERROR.IOError;
                }
            }
            catch (Exception e)
            {
                if (readerDevice != null)
                {
                    readerDevice.Dispose();
                }

                LogWriter.CreateLogEntry(e, FacilityName);

                return ERROR.IOError;
            }
        }
    }

## Contributors ✨

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>  
    <tbody>    
        <tr>      
            <td align="center" valign="top" width="14.28%">        
                <a href="https://github.com/c3rebro">
                    <img src="https://avatars.githubusercontent.com/u/5468524?v=4?s=100" width="100px;" alt="Steven (c3rebro)"/><br />        
                    <sub><b>Steven (c3rebro)</b></sub></a><br />        
                <a href="#code" title="Code">💻</a> 
            <td align="center" valign="top" width="14.28%">
                <a href="https://github.com/faiteanu">
                    <img src="https://avatars.githubusercontent.com/u/63024793?v=4?s=100" width="100px;" alt="Fabian Aiteanu"/><br />      
                    <sub><b>Fabian Aiteanu</b></sub></a><br />
                <a href="#code" title="Code">💻</a> 
        </tr>  
    </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
