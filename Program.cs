using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using HALCONXLib;
using HalconDotNet;

namespace _25industrial
{
 
        public partial class HDevelopExport
        {
#if !NO_EXPORT_APP_MAIN
            public HDevelopExport()
            {
                // Default settings used in HDevelop 
                HOperatorSetX Op = new HOperatorSetX();
                Op.SetSystem("do_low_error", "false");
                action();
            }
#endif

            public void HDevelopStop()
            {
            }

            const int ErrCodeUserException = 30000;
            public class HException : COMException
            {
                public Object user_data;

                public static int HErrCode(int err)
                {
                    HSystemX sys = new HSystemX();
                    return err + sys.ErrorBaseHalcon;
                }
                public HException(object tuple) :
                  base((string)(((Array)tuple).GetValue(1)),
                  HErrCode(Convert.ToInt32(((Array)tuple).GetValue(0))))
                {
                    HTupleX Tuple = new HTupleX();
                    int user_data_ind = 2;
                    if (Convert.ToInt32(((Array)tuple).GetValue(0)) >= ErrCodeUserException)
                        user_data_ind = 1;
                    if (user_data_ind <= ((Array)tuple).Length - 1)
                        this.user_data = Tuple.TupleSelectRange(tuple, user_data_ind,
                                                                 ((Array)tuple).Length - 1);
                }
            }

            public void ExceptionToTuple(COMException exception, int err,
                                         out object tuple)
            {
                Type ud_t, exc_t = exception.GetType();
                Array user_data;
                string full_name = exc_t.FullName;
                bool is_user_exception = (err >= ErrCodeUserException);
                if (is_user_exception)
                {
                    tuple = new object[1];
                    ((Array)tuple).SetValue(err, 0);
                    ud_t = ((HException)exception).user_data.GetType();
                    if (!ud_t.IsArray)
                    {
                        user_data = new Object[1];
                        user_data.SetValue(((HException)exception).user_data, 0);
                    }
                    else
                        user_data = (Array)((HException)exception).user_data;
                    ExtendVar(ref tuple, user_data.Length);
                    user_data.CopyTo((Array)tuple, 1);
                }
                else
                {
                    tuple = new object[2];
                    ((Array)tuple).SetValue(err, 0);
                    ((Array)tuple).SetValue(exception.Message, 1);
                    if (exc_t.FullName != "System.Runtime.InteropServices.COMException")
                    {
                        ud_t = ((HException)exception).user_data.GetType();
                        if (!ud_t.IsArray)
                        {
                            user_data = new Object[1];
                            user_data.SetValue(((HException)exception).user_data, 0);
                        }
                        else
                            user_data = (Array)((HException)exception).user_data;
                        ExtendVar(ref tuple, 1 + user_data.Length);
                        user_data.CopyTo((Array)tuple, 2);
                    }
                }
            }

            public void GetExceptionData(object exception, object name, out object value)
            {
                HSystemX sys = new HSystemX();
                HTupleX Tuple = new HTupleX();
                value = new object[0];
                int err;
                Array name2;
                Type exc_t = exception.GetType();
                Type name_t = name.GetType();
                if (!name_t.IsArray)
                {
                    name2 = new object[1];
                    name2.SetValue(name, 0);
                }
                else
                    name2 = (Array)name;
                if (!exc_t.IsArray)
                    err = Convert.ToInt32(exception);
                else
                    err = Convert.ToInt32(((Array)exception).GetValue(0));

                // error number > 30000 -> user defined exception
                bool is_user_exc = (err >= ErrCodeUserException);

                int n = ((Array)name2).Length;
                for (int i = 0; i < n; i++)
                {
                    if (name2.GetValue(i).GetType().FullName != "System.String")
                    {
                        throw new HException(Tuple.TupleConcat(-sys.ErrorBaseHalcon,
                                             "GetExceptionData(): wrong type " +
                                             "of input parameter 'name'."));
                    }
                    int slot_idx = -1;
                    string slot_name = (string)name2.GetValue(i);

                    if (slot_name == "error_code")
                        slot_idx = 0;
                    else if (slot_name == "add_error_code")
                        slot_idx = -1;
                    else if (slot_name == "user_data")
                    {
                        // user data is a tuple in general -> do not request together with other
                        // slots
                        if (n != 1)
                        {
                            throw new HException(Tuple.TupleConcat(-sys.ErrorBaseHalcon,
                                                 "GetExceptionData(): slot 'user_data' on" +
                                                 "parameter 'Name' cannot be requested " +
                                                 "together with other slots."));
                        }
                        // user defined exception -> return everything but error number
                        //                   else -> return everything that is user defined
                        if (is_user_exc)
                            slot_idx = 1;
                        else
                            slot_idx = 2;
                        if (slot_idx <= ((Array)exception).Length - 1)
                            value = Tuple.TupleSelectRange(exception, slot_idx,
                                                 ((Array)exception).Length - 1);
                        break;
                    }
                    else if (slot_name == "error_msg")
                        slot_idx = 1;
                    else if (slot_name == "add_error_msg")
                        slot_idx = -1;
                    else if (slot_name == "proc_line")
                        slot_idx = -1;
                    else if (slot_name == "operator")
                        slot_idx = -1;
                    else if (slot_name == "call_stack_depth")
                        slot_idx = -1;
                    else if (slot_name == "procedure")
                        slot_idx = -1;
                    else
                    {
                        throw new HException(Tuple.TupleConcat(-sys.ErrorBaseHalcon,
                                             "wrong value of input parameter 'name'."));
                    }
                    ExtendVar(ref value, i);
                    // undefined slot -> return empty string
                    if (slot_idx == -1)
                        ((Array)value).SetValue("", i);
                    else if (is_user_exc && (slot_idx != 0))
                        ((Array)value).SetValue("User defined exception", i);
                    else
                    {
                        if (!exc_t.IsArray)
                            ((Array)value).SetValue(exception, i);
                        else
                            ((Array)value).SetValue(((Array)exception).GetValue(slot_idx), i);
                    }
                }
            }
            public void ExtendVar(ref object values, int index)
            {
                if (values == null)
                    values = new object[index + 1];
                else
                {
                    Type t = values.GetType();
                    if (!t.IsArray)
                    {
                        object tmp = values;
                        values = new object[index + 1];
                        ((Array)values).SetValue(tmp, 0);
                    }
                    else
                    {
                        int len = ((Array)values).Length;
                        if (index >= len)
                        {
                            object new_arr = new object[index + 1];
                            Array.Copy((Array)values, (Array)new_arr, len);
                            values = new_arr;
                        }
                        else
                        {
                            if (t.FullName != "System.Object[]")
                            {
                                object new_arr = new object[len];
                                Array.Copy((Array)values, (Array)new_arr, len);
                                values = new_arr;
                            }
                        }
                    }
                }
            }

            // Procedures 
            // External procedures 
            // Chapter: Graphics / Text
            // Short Description: This procedure writes a text message.
            public void disp_message(object hv_WindowHandle, object hv_String, object hv_CoordSystem,
                object hv_Row, object hv_Column, object hv_Color, object hv_Box)
            {
                HOperatorSetX Op = new HOperatorSetX();
                HTupleX Tuple = new HTupleX();
                HDevWindowStackX COMExpWinHandleStack = new HDevWindowStackX();
                HSystemX sys = new HSystemX();

                // Local control variables 

                object hv_Red, hv_Green, hv_Blue, hv_Row1Part;
                object hv_Column1Part, hv_Row2Part, hv_Column2Part, hv_RowWin;
                object hv_ColumnWin, hv_WidthWin, hv_HeightWin, hv_MaxAscent;
                object hv_MaxDescent, hv_MaxWidth, hv_MaxHeight, hv_R1 = null;
                object hv_C1 = null, hv_FactorRow = null, hv_FactorColumn = null;
                object hv_Width = null, hv_Index = null, hv_Ascent = null, hv_Descent = null;
                object hv_W = null, hv_H = null, hv_FrameHeight = null, hv_FrameWidth = null;
                object hv_R2 = null, hv_C2 = null, hv_DrawMode = null, hv_Exception = null;
                object hv_CurrentColor = null;

                // Initialize local and output iconic variables 

                //This procedure displays text in a graphics window.
                //
                //Input parameters:
                //WindowHandle: The WindowHandle of the graphics window, where
                //   the message should be displayed
                //String: A tuple of strings containing the text message to be displayed
                //CoordSystem: If set to 'window', the text position is given
                //   with respect to the window coordinate system.
                //   If set to 'image', image coordinates are used.
                //   (This may be useful in zoomed images.)
                //Row: The row coordinate of the desired text position
                //   If set to -1, a default value of 12 is used.
                //Column: The column coordinate of the desired text position
                //   If set to -1, a default value of 12 is used.
                //Color: defines the color of the text as string.
                //   If set to [], '' or 'auto' the currently set color is used.
                //   If a tuple of strings is passed, the colors are used cyclically
                //   for each new textline.
                //Box: If set to 'true', the text is written within a white box.
                //
                //prepare window
                Op.GetRgb(hv_WindowHandle, out hv_Red, out hv_Green, out hv_Blue);
                Op.GetPart(hv_WindowHandle, out hv_Row1Part, out hv_Column1Part, out hv_Row2Part,
                    out hv_Column2Part);
                Op.GetWindowExtents(hv_WindowHandle, out hv_RowWin, out hv_ColumnWin, out hv_WidthWin,
                    out hv_HeightWin);
                Op.SetPart(hv_WindowHandle, 0, 0, Tuple.TupleSub(hv_HeightWin, 1), Tuple.TupleSub(
                    hv_WidthWin, 1));
                //
                //default settings
                if (Convert.ToInt32(Tuple.TupleEqual(hv_Row, -1)) != 0)
                {
                    hv_Row = 12;
                }
                if (Convert.ToInt32(Tuple.TupleEqual(hv_Column, -1)) != 0)
                {
                    hv_Column = 12;
                }
                if (Convert.ToInt32(Tuple.TupleEqual(hv_Color, null)) != 0)
                {
                    hv_Color = "";
                }
                //
                hv_String = Tuple.TupleSplit(Tuple.TupleAdd(Tuple.TupleAdd("", hv_String), ""),
                    "\n");
                //
                //Estimate extentions of text depending on font size.
                Op.GetFontExtents(hv_WindowHandle, out hv_MaxAscent, out hv_MaxDescent, out hv_MaxWidth,
                    out hv_MaxHeight);
                if (Convert.ToInt32(Tuple.TupleEqual(hv_CoordSystem, "window")) != 0)
                {
                    hv_R1 = hv_Row;
                    hv_C1 = hv_Column;
                }
                else
                {
                    //transform image to window coordinates
                    hv_FactorRow = Tuple.TupleDiv(Tuple.TupleMult(1.0, hv_HeightWin), Tuple.TupleAdd(
                        Tuple.TupleSub(hv_Row2Part, hv_Row1Part), 1));
                    hv_FactorColumn = Tuple.TupleDiv(Tuple.TupleMult(1.0, hv_WidthWin), Tuple.TupleAdd(
                        Tuple.TupleSub(hv_Column2Part, hv_Column1Part), 1));
                    hv_R1 = Tuple.TupleMult(Tuple.TupleAdd(Tuple.TupleSub(hv_Row, hv_Row1Part), 0.5),
                        hv_FactorRow);
                    hv_C1 = Tuple.TupleMult(Tuple.TupleAdd(Tuple.TupleSub(hv_Column, hv_Column1Part),
                        0.5), hv_FactorColumn);
                }
                //
                //display text box depending on text size
                if (Convert.ToInt32(Tuple.TupleEqual(hv_Box, "true")) != 0)
                {
                    //calculate box extents
                    hv_String = Tuple.TupleAdd(Tuple.TupleAdd(" ", hv_String), " ");
                    hv_Width = null;
                    for (hv_Index = 0; Convert.ToInt32(hv_Index) <= Convert.ToInt32(Tuple.TupleSub(
                        Tuple.TupleLength(hv_String), 1)); hv_Index = Convert.ToInt32(hv_Index) + 1)
                    {
                        Op.GetStringExtents(hv_WindowHandle, Tuple.TupleSelect(hv_String, hv_Index),
                            out hv_Ascent, out hv_Descent, out hv_W, out hv_H);
                        hv_Width = Tuple.TupleConcat(hv_Width, hv_W);
                    }
                    hv_FrameHeight = Tuple.TupleMult(hv_MaxHeight, Tuple.TupleLength(hv_String));
                    hv_FrameWidth = Tuple.TupleMax(Tuple.TupleConcat(0, hv_Width));
                    hv_R2 = Tuple.TupleAdd(hv_R1, hv_FrameHeight);
                    hv_C2 = Tuple.TupleAdd(hv_C1, hv_FrameWidth);
                    //display rectangles
                    Op.GetDraw(hv_WindowHandle, out hv_DrawMode);
                    Op.SetDraw(hv_WindowHandle, "fill");
                    Op.SetColor(hv_WindowHandle, "light gray");
                    Op.DispRectangle1(hv_WindowHandle, Tuple.TupleAdd(hv_R1, 3), Tuple.TupleAdd(
                        hv_C1, 3), Tuple.TupleAdd(hv_R2, 3), Tuple.TupleAdd(hv_C2, 3));
                    Op.SetColor(hv_WindowHandle, "white");
                    Op.DispRectangle1(hv_WindowHandle, hv_R1, hv_C1, hv_R2, hv_C2);
                    Op.SetDraw(hv_WindowHandle, hv_DrawMode);
                }
                else if (Convert.ToInt32(Tuple.TupleNotEqual(hv_Box, "false")) != 0)
                {
                    hv_Exception = "Wrong value of control parameter Box";
                    throw new HException(hv_Exception);
                }
                //Write text.
                for (hv_Index = 0; Convert.ToInt32(hv_Index) <= Convert.ToInt32(Tuple.TupleSub(Tuple.TupleLength(
                    hv_String), 1)); hv_Index = Convert.ToInt32(hv_Index) + 1)
                {
                    hv_CurrentColor = Tuple.TupleSelect(hv_Color, Tuple.TupleMod(hv_Index, Tuple.TupleLength(
                        hv_Color)));
                    if (Convert.ToInt32(Tuple.TupleAnd(Tuple.TupleNotEqual(hv_CurrentColor, ""),
                        Tuple.TupleNotEqual(hv_CurrentColor, "auto"))) != 0)
                    {
                        Op.SetColor(hv_WindowHandle, hv_CurrentColor);
                    }
                    else
                    {
                        Op.SetRgb(hv_WindowHandle, hv_Red, hv_Green, hv_Blue);
                    }
                    hv_Row = Tuple.TupleAdd(hv_R1, Tuple.TupleMult(hv_MaxHeight, hv_Index));
                    Op.SetTposition(hv_WindowHandle, hv_Row, hv_C1);
                    Op.WriteString(hv_WindowHandle, Tuple.TupleSelect(hv_String, hv_Index));
                }
                //reset changed window settings
                Op.SetRgb(hv_WindowHandle, hv_Red, hv_Green, hv_Blue);
                Op.SetPart(hv_WindowHandle, hv_Row1Part, hv_Column1Part, hv_Row2Part, hv_Column2Part);

                return;
            }

            // Chapter: Graphics / Text
            // Short Description: Set font independent of OS
            public void set_display_font(object hv_WindowHandle, object hv_Size, object hv_Font,
                object hv_Bold, object hv_Slant)
            {
                HOperatorSetX Op = new HOperatorSetX();
                HTupleX Tuple = new HTupleX();
                HDevWindowStackX COMExpWinHandleStack = new HDevWindowStackX();
                HSystemX sys = new HSystemX();

                // Local control variables 

                object hv_OS, hv_Exception = null, hv_AllowedFontSizes = null;
                object hv_Distances = null, hv_Indices = null;

                // Initialize local and output iconic variables 

                //This procedure sets the text font of the current window with
                //the specified attributes.
                //It is assumed that following fonts are installed on the system:
                //Windows: Courier New, Arial Times New Roman
                //Linux: courier, helvetica, times
                //Because fonts are displayed smaller on Linux than on Windows,
                //a scaling factor of 1.25 is used the get comparable results.
                //For Linux, only a limited number of font sizes is supported,
                //to get comparable results, it is recommended to use one of the
                //following sizes: 9, 11, 14, 16, 20, 27
                //(which will be mapped internally on Linux systems to 11, 14, 17, 20, 25, 34)
                //
                //input parameters:
                //WindowHandle: The graphics window for which the font will be set
                //Size: The font size. If Size=-1, the default of 16 is used.
                //Bold: If set to 'true', a bold font is used
                //Slant: If set to 'true', a slanted font is used
                //
                Op.GetSystem("operating_system", out hv_OS);
                if (Convert.ToInt32(Tuple.TupleOr(Tuple.TupleEqual(hv_Size, null), Tuple.TupleEqual(
                    hv_Size, -1))) != 0)
                {
                    hv_Size = 16;
                }
                if (Convert.ToInt32(Tuple.TupleEqual(Tuple.TupleStrLastN(Tuple.TupleStrFirstN(
                    hv_OS, 2), 0), "Win")) != 0)
                {
                    //set font on Windows systems
                    if (Convert.ToInt32(Tuple.TupleOr(Tuple.TupleOr(Tuple.TupleEqual(hv_Font, "mono"),
                        Tuple.TupleEqual(hv_Font, "Courier")), Tuple.TupleEqual(hv_Font, "courier"))) != 0)
                    {
                        hv_Font = "Courier New";
                    }
                    else if (Convert.ToInt32(Tuple.TupleEqual(hv_Font, "sans")) != 0)
                    {
                        hv_Font = "Arial";
                    }
                    else if (Convert.ToInt32(Tuple.TupleEqual(hv_Font, "serif")) != 0)
                    {
                        hv_Font = "Times New Roman";
                    }
                    if (Convert.ToInt32(Tuple.TupleEqual(hv_Bold, "true")) != 0)
                    {
                        hv_Bold = 1;
                    }
                    else if (Convert.ToInt32(Tuple.TupleEqual(hv_Bold, "false")) != 0)
                    {
                        hv_Bold = 0;
                    }
                    else
                    {
                        hv_Exception = "Wrong value of control parameter Bold";
                        throw new HException(hv_Exception);
                    }
                    if (Convert.ToInt32(Tuple.TupleEqual(hv_Slant, "true")) != 0)
                    {
                        hv_Slant = 1;
                    }
                    else if (Convert.ToInt32(Tuple.TupleEqual(hv_Slant, "false")) != 0)
                    {
                        hv_Slant = 0;
                    }
                    else
                    {
                        hv_Exception = "Wrong value of control parameter Slant";
                        throw new HException(hv_Exception);
                    }
                    try
                    {
                        Op.SetFont(hv_WindowHandle, Tuple.TupleAdd(Tuple.TupleAdd(Tuple.TupleAdd(
                            Tuple.TupleAdd(Tuple.TupleAdd(Tuple.TupleAdd(Tuple.TupleAdd(Tuple.TupleAdd(
                            "-", hv_Font), "-"), hv_Size), "-*-"), hv_Slant), "-*-*-"), hv_Bold), "-"));
                    }
                    // catch (Exception) 
                    catch (COMException HDevExpDefaultException1)
                    {
                        ExceptionToTuple(HDevExpDefaultException1, (int)HDevExpDefaultException1.ErrorCode - sys.ErrorBaseHalcon, out hv_Exception);
                        throw new HException(hv_Exception);
                    }
                }
                else
                {
                    //set font for UNIX systems
                    hv_Size = Tuple.TupleMult(hv_Size, 1.25);
                    hv_AllowedFontSizes = Tuple.TupleConcat(Tuple.TupleConcat(Tuple.TupleConcat(
                        Tuple.TupleConcat(Tuple.TupleConcat(11, 14), 17), 20), 25), 34);
                    if (Convert.ToInt32(Tuple.TupleEqual(Tuple.TupleFind(hv_AllowedFontSizes, hv_Size),
                        -1)) != 0)
                    {
                        hv_Distances = Tuple.TupleAbs(Tuple.TupleSub(hv_AllowedFontSizes, hv_Size));
                        Op.TupleSortIndex(hv_Distances, out hv_Indices);
                        hv_Size = Tuple.TupleSelect(hv_AllowedFontSizes, Tuple.TupleSelect(hv_Indices,
                            0));
                    }
                    if (Convert.ToInt32(Tuple.TupleOr(Tuple.TupleEqual(hv_Font, "mono"), Tuple.TupleEqual(
                        hv_Font, "Courier"))) != 0)
                    {
                        hv_Font = "courier";
                    }
                    else if (Convert.ToInt32(Tuple.TupleEqual(hv_Font, "sans")) != 0)
                    {
                        hv_Font = "helvetica";
                    }
                    else if (Convert.ToInt32(Tuple.TupleEqual(hv_Font, "serif")) != 0)
                    {
                        hv_Font = "times";
                    }
                    if (Convert.ToInt32(Tuple.TupleEqual(hv_Bold, "true")) != 0)
                    {
                        hv_Bold = "bold";
                    }
                    else if (Convert.ToInt32(Tuple.TupleEqual(hv_Bold, "false")) != 0)
                    {
                        hv_Bold = "medium";
                    }
                    else
                    {
                        hv_Exception = "Wrong value of control parameter Bold";
                        throw new HException(hv_Exception);
                    }
                    if (Convert.ToInt32(Tuple.TupleEqual(hv_Slant, "true")) != 0)
                    {
                        if (Convert.ToInt32(Tuple.TupleEqual(hv_Font, "times")) != 0)
                        {
                            hv_Slant = "i";
                        }
                        else
                        {
                            hv_Slant = "o";
                        }
                    }
                    else if (Convert.ToInt32(Tuple.TupleEqual(hv_Slant, "false")) != 0)
                    {
                        hv_Slant = "r";
                    }
                    else
                    {
                        hv_Exception = "Wrong value of control parameter Slant";
                        throw new HException(hv_Exception);
                    }
                    try
                    {
                        Op.SetFont(hv_WindowHandle, Tuple.TupleAdd(Tuple.TupleAdd(Tuple.TupleAdd(
                            Tuple.TupleAdd(Tuple.TupleAdd(Tuple.TupleAdd(Tuple.TupleAdd(Tuple.TupleAdd(
                            "-adobe-", hv_Font), "-"), hv_Bold), "-"), hv_Slant), "-normal-*-"), hv_Size),
                            "-*-*-*-*-*-*-*"));
                    }
                    // catch (Exception) 
                    catch (COMException HDevExpDefaultException1)
                    {
                        ExceptionToTuple(HDevExpDefaultException1, (int)HDevExpDefaultException1.ErrorCode - sys.ErrorBaseHalcon, out hv_Exception);
                        throw new HException(hv_Exception);
                    }
                }

                return;
            }

            // Main procedure 
            public void action()
            {
                HOperatorSetX Op = new HOperatorSetX();
                HTupleX Tuple = new HTupleX();
                HDevWindowStackX COMExpWinHandleStack = new HDevWindowStackX();

                // Local iconic variables 

                HUntypedObjectX ho_Image = null, ho_SymbolRegions = null;


                // Local control variables 

                object hv_BarCodeHandle, hv_WindowHandle, hv_I;
                object hv_Width = null, hv_Height = null, hv_DecodedDataStrings = null;
                object hv_LastChar = null;

                // Initialize local and output iconic variables 
                Op.GenEmptyObj(out ho_Image);
                Op.GenEmptyObj(out ho_SymbolRegions);

                //Read bar codes of type 2/5 Industrial
                //
                Op.CreateBarCodeModel(null, null, out hv_BarCodeHandle);
                if (COMExpWinHandleStack.IsOpen() != 0)
                {
                    Op.CloseWindow(COMExpWinHandleStack.Pop());
                }
                Op.SetWindowAttr("background_color", "black");
                Op.OpenWindow(0, 0, 120, 300, 0, "", "", out hv_WindowHandle);
                COMExpWinHandleStack.Push(hv_WindowHandle);
                set_display_font(hv_WindowHandle, 14, "mono", "true", "false");
                if (COMExpWinHandleStack.IsOpen() != 0)
                {
                    Op.SetDraw(COMExpWinHandleStack.GetActive(), "margin");
                }
                if (COMExpWinHandleStack.IsOpen() != 0)
                {
                    Op.SetLineWidth(COMExpWinHandleStack.GetActive(), 3);
                }
                for (hv_I = 1; Convert.ToInt32(hv_I) <= 4; hv_I = Convert.ToInt32(hv_I) + 1)
                {
                    Marshal.ReleaseComObject(ho_Image);
                    Op.GenEmptyObj(out ho_Image);
                    Op.ReadImage(out ho_Image, Tuple.TupleAdd("I:/soft/halcon 10.0/images/barcode/25industrial/25industrial0", hv_I));
                    Op.GetImageSize(ho_Image, out hv_Width, out hv_Height);
                    if (COMExpWinHandleStack.IsOpen() != 0)
                    {
                        Op.SetWindowExtents(COMExpWinHandleStack.GetActive(), 0, 0, Tuple.TupleSub(
                            hv_Width, 1), Tuple.TupleSub(hv_Height, 1));
                    }
                    if (COMExpWinHandleStack.IsOpen() != 0)
                    {
                        Op.DispObj(ho_Image, COMExpWinHandleStack.GetActive());
                    }
                    if (COMExpWinHandleStack.IsOpen() != 0)
                    {
                        Op.SetColor(COMExpWinHandleStack.GetActive(), "green");
                    }
                    //Read bar code, the resulting string includes the check character
                    Op.SetBarCodeParam(hv_BarCodeHandle, "check_char", "absent");
                    Marshal.ReleaseComObject(ho_SymbolRegions);
                    Op.FindBarCode(ho_Image, out ho_SymbolRegions, hv_BarCodeHandle, "2/5 Industrial",
                        out hv_DecodedDataStrings);
                    disp_message(hv_WindowHandle, hv_DecodedDataStrings, "window", 12, 12, "black",
                        "false");
                    hv_LastChar = Tuple.TupleSub(Tuple.TupleStrlen(hv_DecodedDataStrings), 1);
                    disp_message(hv_WindowHandle, Tuple.TupleAdd(Tuple.TupleSum(Tuple.TupleGenConst(
                        hv_LastChar, " ")), Tuple.TupleStrBitSelect(hv_DecodedDataStrings, hv_LastChar)),
                        "window", 12, 12, "forest green", "false");
                    HDevelopStop();
                    //Read bar code using the check character to check the result, i.e.,
                    //the check character does not belong to the returned string anymore.
                    //If the check character is not correct, the bar code reading fails
                    if (COMExpWinHandleStack.IsOpen() != 0)
                    {
                        Op.SetColor(COMExpWinHandleStack.GetActive(), "green");
                    }
                    Op.SetBarCodeParam(hv_BarCodeHandle, "check_char", "present");
                    Marshal.ReleaseComObject(ho_SymbolRegions);
                    Op.FindBarCode(ho_Image, out ho_SymbolRegions, hv_BarCodeHandle, "2/5 Industrial",
                        out hv_DecodedDataStrings);
                    disp_message(hv_WindowHandle, hv_DecodedDataStrings, "window", 36, 12, "black",
                        "false");
                    if (COMExpWinHandleStack.IsOpen() != 0)
                    {
                        Op.SetColor(COMExpWinHandleStack.GetActive(), "magenta");
                    }
                    if (Convert.ToInt32(Tuple.TupleLess(hv_I, 4)) != 0)
                    {
                        HDevelopStop();
                    }
                }
                Op.ClearBarCodeModel(hv_BarCodeHandle);
                Marshal.ReleaseComObject(ho_Image);
                Marshal.ReleaseComObject(ho_SymbolRegions);

            }


        }



    static class Program
    {
 
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }

}
