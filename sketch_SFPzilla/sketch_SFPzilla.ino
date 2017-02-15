
/**********************************************/
/*             I N C L U D E S                */
/**********************************************/
#include "Wire.h"
#include <stdint.h>

#include <avr/pgmspace.h>
#include <Arduino.h>
#include "HardwareSerial.h"


// command line structure
typedef struct _cmd_t
{
    char *cmd;
    void (*func)(int argc, char **argv);
    struct _cmd_t *next;
} cmd_t;

void cmdInit(Stream *);
void cmdPoll();
void cmdAdd(char *name, void (*func)(int argc, char **argv));
int cmdStr2Num(char *str, uint8_t base, uint16_t *val);



#define MAX_MSG_SIZE    100

// command line message buffer and pointer
static uint8_t msg[MAX_MSG_SIZE];
static uint8_t *msg_ptr;

// linked list for command table
static cmd_t *cmd_tbl_list, *cmd_tbl;

// text strings for command prompt (stored in flash)

const char cmd_unrecog[] PROGMEM = "FAIL";
const char str_version[] PROGMEM = "Version 1.0";
const char str_help1[] PROGMEM = "ping";
const char str_help2[] PROGMEM = "device <DD>   (set i2c device address to 0x<DD>)";
const char str_help3[] PROGMEM = "offset <OO>   (set i2c mem offset to 0x<OO>)";
const char str_help3a[] PROGMEM = "conf          (print current device/offset config)";
const char str_help4[] PROGMEM = "power on      (set the SFP power ON)";
const char str_help5[] PROGMEM = "power off     (set the SFP power OFF)";
const char str_help6[] PROGMEM = "read <NN>     (read 0x<NN> bytes from SFP device 0x<DD> at offset 0x<OO>";
const char str_help7[] PROGMEM = "write <dd> <dd> ... (write data <dd>.. to SFP device 0x<DD> at offset 0x<OO> - max. 16 bytes";
const char str_help8[] PROGMEM = "dump_bank_a0  (dump standard SFP bank A0 - device=0x50)";
const char str_help9[] PROGMEM = "dump_bank_a2  (dump standard SFP bank A2 - device=0x51)";
const char str_help10[] PROGMEM = "dump_bank_b0  (dump standard SFP bank B0 - device=0x58)";
const char str_help11[] PROGMEM = "dump_bank_b2  (dump standard SFP bank B2 - device=0x59)";
const char str_help12[] PROGMEM = "echo on       (set the echo for command ON)";
const char str_help13[] PROGMEM = "echo off      (set the echo for command OFF)";
const char str_help14[] PROGMEM = "version";
const char str_help15[] PROGMEM = "help";

static Stream* stream;

/**********************************************/
/*             D E F I N E S                  */
/**********************************************/

/* pin definitions */
#define LED_RED_PIN     9
#define LED_GREEN_PIN   8
#define SFP_POWER_PIN   12

/* SFP power mode */
#define SFP_ON      1
#define SFP_OFF     0
/* LED mode */
#define LED_RED     1
#define LED_GREEN   2
#define LED_OFF     0

/* standard SFP I2C addresses */
#define SFP_ADDRESS_A0 0x50
#define SFP_ADDRESS_A2 0x51
#define SFP_ADDRESS_B0 0x58
#define SFP_ADDRESS_B2 0x59

/* responses for commands */
#define STAT_OK           0
#define STAT_FAIL         1
#define STAT_SYNTAX_ERROR 2

/********************************************************/
/*     F U N C T I O N S   D E C L A R A T I O N S      */
/********************************************************/
void  led_ctrl(int mode);
void  pwr_ctrl(int mode);

void getSFPdata(uint8_t sfp_bank, unsigned int offset, byte *a, int *stat);
void i2c_eeprom_write_byte( int deviceaddress, unsigned int eeaddress, byte data );
void i2c_eeprom_write_page( int deviceaddress, unsigned int eeaddresspage, byte* data, byte length );
byte i2c_eeprom_read_byte( int deviceaddress, unsigned int eeaddress, int *stat );

void  arg_display(int arg_cnt, char **args);
void  cmd_ping(int arg_cnt, char **args);
void  cmd_write(int arg_cnt, char **args);
void  cmd_read(int arg_cnt, char **args);
void  cmd_power(int arg_cnt, char **args);
void  cmd_echo(int arg_cnt, char **args);
void  cmd_device(int arg_cnt, char **args);
void  cmd_offset(int arg_cnt, char **args);
void  cmd_A0(int arg_cnt, char **args);
void  cmd_A2(int arg_cnt, char **args);
void  cmd_B0(int arg_cnt, char **args);
void  cmd_B2(int arg_cnt, char **args);
void  cmd_help(int arg_cnt, char **args);
void  cmd_version(int arg_cnt, char **args);
void  cmd_conf(int arg_cnt, char **args);

void  cmd_response(char stat);

void  SFP_DUMP(uint8_t sfp_bank);

/********************************************************/
/*     G L O B A L   D A T A                            */
/********************************************************/

char          c[100];

uint16_t      SFP_address=0;          /* set by cmd_device() */
uint16_t      SFP_mem_offset=0;       /* set by cmd_offset() */
char          SFP_power_state=SFP_OFF;
char          LED_color_state=LED_OFF;

int           echo_ctrl = 1;

/********************************************************/
/*     F U N C T I O N S                                */
/********************************************************/

/*****************************************/
/*     SETUP()                           */
/*****************************************/
void setup() 
{
  pinMode(LED_GREEN_PIN,OUTPUT);
  pinMode(LED_RED_PIN,OUTPUT);
  pinMode(SFP_POWER_PIN,OUTPUT);

  pwr_ctrl(SFP_ON);   


  // init the command line and set it for a speed of 115200
  Serial.begin (115200);
  cmdInit(&Serial);

  cmdAdd("ping", cmd_ping);
  cmdAdd("write", cmd_write);
  cmdAdd("read", cmd_read);
  cmdAdd("power", cmd_power);
  cmdAdd("echo", cmd_echo);
  cmdAdd("device", cmd_device);
  cmdAdd("offset", cmd_offset);
  cmdAdd("conf", cmd_conf);
  cmdAdd("dump_bank_a0", cmd_A0);
  cmdAdd("dump_bank_a2", cmd_A2);
  cmdAdd("dump_bank_b0", cmd_B0);
  cmdAdd("dump_bank_b2", cmd_B2);
  cmdAdd("help", cmd_help);
  cmdAdd("version", cmd_version);
  cmd_response(STAT_OK);

  Wire.begin();     /* Init i2c */

}

/*****************************************/
/*     LOOP()                            */
/*****************************************/
void loop() 
{
  led_ctrl(LED_GREEN); 
  cmdPoll();
}/* loop() */

/*****************************************/
/*     cmd_reaponse()                    */
/*****************************************/
void  cmd_response(char stat)
{
  if( stat==STAT_OK )
    stream->println("OK");
   if( stat==STAT_FAIL )
    stream->println("FAIL");
  if( stat==STAT_SYNTAX_ERROR )
    stream->println("SYNTAX_ERROR");
}

/*****************************************/
/*     cmd_conf()                        */
/*****************************************/
void  cmd_conf(int arg_cnt, char **args)
{
  led_ctrl(LED_RED);
  delay(100);
  if( arg_cnt!=1 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  sprintf(c, "device:%02x, offset:%02x", SFP_address, SFP_mem_offset );
  stream->println(c);  
  cmd_response(STAT_OK);     
}
/*****************************************/
/*     cmd_version()                        */
/*****************************************/
void  cmd_version(int arg_cnt, char **args)
{
char buf[40];

  led_ctrl(LED_RED);
  delay(100);
  
  if( arg_cnt!=1 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  strcpy_P(buf, str_version);
  stream->print(buf);
  sprintf(buf, ", Created %s", __DATE__);
  stream->println(buf);  
  cmd_response(STAT_OK);  
}
/*****************************************/
/*     cmd_help()                        */
/*****************************************/
void  cmd_help(int arg_cnt, char **args)
{
    char buf[100];

  led_ctrl(LED_RED);
  delay(100);
  
  if( arg_cnt!=1 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
    stream->println();

    strcpy_P(buf, str_help1);
    stream->println(buf);
    strcpy_P(buf, str_help2);
    stream->println(buf);
    strcpy_P(buf, str_help3);
    stream->println(buf);
    strcpy_P(buf, str_help3a);
    stream->println(buf);
    strcpy_P(buf, str_help4);
    stream->println(buf);
    strcpy_P(buf, str_help5);
    stream->println(buf);
    strcpy_P(buf, str_help6);
    stream->println(buf);
    strcpy_P(buf, str_help7);
    stream->println(buf);
    strcpy_P(buf, str_help8);
    stream->println(buf);
    strcpy_P(buf, str_help9);
    stream->println(buf);
    strcpy_P(buf, str_help10);
    stream->println(buf);
    strcpy_P(buf, str_help11);
    stream->println(buf);
    strcpy_P(buf, str_help12);
    stream->println(buf);
    strcpy_P(buf, str_help13);
    stream->println(buf);
    strcpy_P(buf, str_help14);
    stream->println(buf);
    strcpy_P(buf, str_help15);
    stream->println(buf);
    cmd_response(STAT_OK);
}

/*****************************************/
/*     cmd_ping()                        */
/*****************************************/
void  cmd_ping(int arg_cnt, char **args)
{
  led_ctrl(LED_RED);
  delay(10);
  cmd_response(STAT_OK);    /* just set OK status */
}

/*****************************************/
/*     cmd_device()                      */
/*****************************************/
void  cmd_device(int arg_cnt, char **args)
{
int stat;
  
  led_ctrl(LED_RED);
  delay(10);
  if( arg_cnt!=2 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  stat = cmdStr2Num(args[1], 16, &SFP_address);
  if( stat )
  {
    cmd_response(STAT_FAIL);
    return;    
  }
  //sprintf(c, "SFP_address=0x%02x", SFP_address);
  //stream->println(c);  
  if( SFP_address > 0xff )
  {
    cmd_response(STAT_FAIL);
    return;    
  }
  cmd_response(STAT_OK);
}

/*****************************************/
/*     cmd_offset()                      */
/*****************************************/
void  cmd_offset(int arg_cnt, char **args)
{
int  stat;

  led_ctrl(LED_RED);
  delay(10);
  if( arg_cnt!=2 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  stat = cmdStr2Num(args[1], 16, &SFP_mem_offset);
  if( stat )
  {
    cmd_response(STAT_FAIL);
    return;    
  }
  //sprintf(c, "SFP_mem_offset = 0x%02x\n\r", SFP_mem_offset);
  //Serial.print(c);  
  if( SFP_mem_offset > 0xff )
  {
    cmd_response(STAT_FAIL);
    return;    
  }
  cmd_response(STAT_OK);
}
/*****************************************/
/*     cmd_write()                       */
/*****************************************/
void  cmd_write(int arg_cnt, char **args)
{
int       stat;
int       i;
uint16_t  dat[20];
byte      tmp;

  led_ctrl(LED_RED);
  delay(100);
  if( SFP_power_state!=SFP_ON )
  {
    cmd_response(STAT_FAIL);
    return;    
  }
  if( arg_cnt==1 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  if( arg_cnt>17 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  //stream->println(">");
  /* check the arguments */
  for (i=1; i<arg_cnt; i++)
  {
    stat = cmdStr2Num(args[i], 16, &dat[i]);  /* Convert hex string to integer */
    if( stat )
    {
      cmd_response(STAT_FAIL);
      return;    
    }
    //sprintf(c, "CNV dat[%d]=0x%02x", i, dat[i]);
    //stream->println(c); 
  }/* for */
  
  /* Write the data into SFP */
  for (i=1; i<arg_cnt; i++)
  {
    //sprintf(c, "WRITE dat[%d]=0x%02x", i, dat[i]);
    //stream->println(c);

     /* check if device exists  */
    tmp = i2c_eeprom_read_byte( SFP_address, SFP_mem_offset+i-1, &stat );
    if( stat )
    {
      cmd_response(STAT_FAIL);
      return;    
    }
    i2c_eeprom_write_byte( SFP_address, SFP_mem_offset+i-1, (byte)dat[i] );
    delay(20);      /* Important delay !!!! */

    /* verify written data */
    tmp = i2c_eeprom_read_byte( SFP_address, SFP_mem_offset+i-1, &stat );
    if( stat )
    {
      cmd_response(STAT_FAIL);
      return;    
    }
    //sprintf(c, "READ dat[%d]=0x%02x", i, tmp);
    //stream->println(c);
    if( tmp != (byte)dat[i] )
    {
      cmd_response(STAT_FAIL);
      return;    
   
    }
  }/* for */
  
  cmd_response(STAT_OK);
}

/*****************************************/
/*     cmd_read()                        */
/*****************************************/
void  cmd_read(int arg_cnt, char **args)
{
int       stat;
uint16_t  size;
byte      dat;
int       i;

  led_ctrl(LED_RED);
  delay(100);
  if( SFP_power_state!=SFP_ON )
  {
    cmd_response(STAT_FAIL);
    return;    
  }
  if( arg_cnt!=2 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  stat = cmdStr2Num(args[1], 16, &size);
  if( stat )
  {
    cmd_response(STAT_FAIL);
    return;    
  }

  if( size==0x00 ) size=0x100;
  
  if( (SFP_mem_offset+size) > 0x100 )
  {
    size = 0x100-SFP_mem_offset;
  }
  if( size==0 )
  {
    cmd_response(STAT_OK);
    return;    
  }
  //sprintf(c, "size=0x%02x\n\r", size);
  //stream->print(c); 
  for( i=SFP_mem_offset; i<(SFP_mem_offset+size); i++ )
  {
          getSFPdata(SFP_address, i, &dat, &stat);
          if( stat )
          {
            cmd_response(STAT_FAIL);
            return;    
          }
          sprintf(c, "%02x ", dat);  
          stream->print(c);   
          //stream->print(" ");
         
  }/* for i */
  stream->println(""); 
  cmd_response(STAT_OK);
}

/*****************************************/
/*     SFP_DUMP()                        */
/*****************************************/
void  SFP_DUMP(uint8_t sfp_bank)
{
byte      data[20];
uint8_t   i,j;
int       stat;


      if( SFP_power_state==SFP_ON )
      {
        led_ctrl(LED_RED); 

        for( j=0; j<16; j++ )
        {
          for( i=0; i<16; i++ )
          {
            getSFPdata(sfp_bank, i+j*16, &data[i], &stat);
            if( stat )
            {
              cmd_response(STAT_FAIL);
              return;    
            }
            sprintf(c, "%02x ", data[i]);  
            stream->print(c);   
            //stream->print(" ");
       
        
          }/* for i */
          stream->print(" | ");
          for( i=0; i<16; i++ )
          {
            if( isPrintable(data[i]) )
            {
              sprintf(c, "%c", data[i]);
              stream->print(c);  
            }
            else
              stream->print(".");  
          }
          stream->println("");   // END OF LINE
 
        }/* for j */
        cmd_response(STAT_OK);
      }/* if Power ON */
      else
      {
        cmd_response(STAT_FAIL);
      }/* else power OFF */

  
}
/*****************************************/
/*     cmd_A0()                          */
/*****************************************/
void  cmd_A0(int arg_cnt, char **args)
{
  SFP_DUMP(SFP_ADDRESS_A0);
}

/*****************************************/
/*     cmd_A2()                          */
/*****************************************/
void  cmd_A2(int arg_cnt, char **args)
{
  SFP_DUMP(SFP_ADDRESS_A2);  
}
/*****************************************/
/*     cmd_B0()                          */
/*****************************************/
void  cmd_B0(int arg_cnt, char **args)
{
  SFP_DUMP(SFP_ADDRESS_B0);  
}
/*****************************************/
/*     cmd_B2()                          */
/*****************************************/
void  cmd_B2(int arg_cnt, char **args)
{
  SFP_DUMP(SFP_ADDRESS_B2);  
}
/*****************************************/
/*     cmd_power()                       */
/*****************************************/
void  cmd_power(int arg_cnt, char **args)
{
  led_ctrl(LED_RED);
  delay(100);
  if( arg_cnt!=2 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  if( !strcmp(args[1],"on") )
  {
    pwr_ctrl(SFP_ON);
    cmd_response(STAT_OK);    
  }
  else if( !strcmp(args[1],"off") )
  {
    pwr_ctrl(SFP_OFF);
    cmd_response(STAT_OK);    
  }
  else
      cmd_response(STAT_FAIL);

}

/*****************************************/
/*     cmd_power()                       */
/*****************************************/
void  cmd_echo(int arg_cnt, char **args)
{
  led_ctrl(LED_RED);
  delay(100);
  if( arg_cnt!=2 )
  {
    cmd_response(STAT_FAIL);
    return;
  }
  if( !strcmp(args[1],"on") )
  {
    echo_ctrl = 1;
    cmd_response(STAT_OK);    
  }
  else if( !strcmp(args[1],"off") )
  {
    echo_ctrl = 0;
    cmd_response(STAT_OK);    
  }
  else
      cmd_response(STAT_FAIL);

}
/*****************************************/
/*     led_ctrl()                        */
/*****************************************/
void  led_ctrl(int mode)
{
    LED_color_state = mode;
    if( SFP_power_state == SFP_ON )
    {
      if( mode==LED_RED )
      {
        digitalWrite(LED_RED_PIN, HIGH);
        digitalWrite(LED_GREEN_PIN, LOW);
      }
      else if( mode==LED_GREEN )
      {
        digitalWrite(LED_RED_PIN, LOW);
        digitalWrite(LED_GREEN_PIN, HIGH);
      }
      else
      {
        digitalWrite(LED_RED_PIN, LOW);
        digitalWrite(LED_GREEN_PIN, LOW);
      }
    }
    else
    { /* if no power, both leds are off regardless what do we want to have */
      digitalWrite(LED_RED_PIN, LOW);
      digitalWrite(LED_GREEN_PIN, LOW);
    }
}

/*****************************************/
/*     pwr_ctrl()                        */
/*****************************************/
void  pwr_ctrl(int mode)
{
    SFP_power_state = mode;

    if( mode==SFP_ON )
    {
      digitalWrite(SFP_POWER_PIN, LOW);
      led_ctrl(LED_color_state);
    }
    if( mode==SFP_OFF )
    {
      digitalWrite(SFP_POWER_PIN, HIGH);
      /* turn both leds off, but do not change the variable */
      digitalWrite(LED_RED_PIN, LOW);
      digitalWrite(LED_GREEN_PIN, LOW);
    }
    delay(100);

    
    
}
/********************** getSFPdata() ********************/
void getSFPdata(uint8_t sfp_bank, unsigned int offset, byte *a, int *stat)
{
    *a = i2c_eeprom_read_byte( sfp_bank, offset, stat );
}

/********************** i2c_eeprom_write_byte() ********************/
void i2c_eeprom_write_byte( int deviceaddress, unsigned int eeaddress, byte data ) 
{
    int rdata = data;

    Wire.beginTransmission(deviceaddress);
    //Wire.write((int)(eeaddress >> 8)); // MSB
    Wire.write((int)(eeaddress & 0xFF)); // LSB
    Wire.write(rdata);
    Wire.endTransmission();
  }

/********************** i2c_eeprom_write_page() ********************/
  // WARNING: address is a page address, 6-bit end will wrap around
  // also, data can be maximum of about 30 bytes, because the Wire library has a buffer of 32 bytes
  void i2c_eeprom_write_page( int deviceaddress, unsigned int eeaddresspage, byte* data, byte length )
  {
    Wire.beginTransmission(deviceaddress);
    //Wire.write((int)(eeaddresspage >> 8)); // MSB
    Wire.write((int)(eeaddresspage & 0xFF)); // LSB
    byte c;
    for ( c = 0; c < length; c++)
      Wire.write(data[c]);
    Wire.endTransmission();
  }

/********************** i2c_eeprom_read_byte() ********************/
  byte i2c_eeprom_read_byte( int deviceaddress, unsigned int eeaddress, int *stat ) 
  {
    int   timeout=10;
    byte rdata = 0xFF;
    Wire.beginTransmission(deviceaddress);
    //Wire.write((int)(eeaddress >> 8)); // MSB
    Wire.write((int)(eeaddress & 0xFF)); // LSB
    Wire.endTransmission();
    Wire.requestFrom(deviceaddress,1);
#if 0
    if (Wire.available()) rdata = Wire.read();
#else
    while( !Wire.available() )   /* wait until ready */
    {
      delay(10);
      timeout--;
      if( !timeout )
      {
        *stat = -1;
        return rdata;      /* timeout error */  
      }
    }
    rdata = Wire.read();
#endif
    *stat = 0;
    return rdata;
  }

  // maybe let's not read more than 30 or 32 bytes at a time!
  void i2c_eeprom_read_buffer( int deviceaddress, unsigned int eeaddress, byte *buffer, int length ) 
  {
    Wire.beginTransmission(deviceaddress);
    //Wire.write((int)(eeaddress >> 8)); // MSB
    Wire.write((int)(eeaddress & 0xFF)); // LSB
    Wire.endTransmission();
    Wire.requestFrom(deviceaddress,length);
    int c = 0;
    for ( c = 0; c < length; c++ )
      if (Wire.available()) buffer[c] = Wire.read();
  }

/**************************************************************************/
/*!
    Parse the command line. This function tokenizes the command input, then
    searches for the command table entry associated with the commmand. Once found,
    it will jump to the corresponding function.
*/
/**************************************************************************/
void cmd_parse(char *cmd)
{
    uint8_t argc, i = 0;
    char *argv[30];
    char buf[100];
    cmd_t *cmd_entry;

    fflush(stdout);

    // parse the command line statement and break it up into space-delimited
    // strings. the array of strings will be saved in the argv array.
    argv[i] = strtok(cmd, " ");
    do
    {
        argv[++i] = strtok(NULL, " ");
    } while ((i < 30) && (argv[i] != NULL));

    // save off the number of arguments for the particular command.
    argc = i;

    // parse the command table for valid command. used argv[0] which is the
    // actual command name typed in at the prompt
    for (cmd_entry = cmd_tbl; cmd_entry != NULL; cmd_entry = cmd_entry->next)
    {
        if (!strcmp(argv[0], cmd_entry->cmd))
        {
            cmd_entry->func(argc, argv);
            //cmd_display();
            return;
        }
    }

    // command not recognized. print message and re-generate prompt.
    strcpy_P(buf, cmd_unrecog);
    stream->println(buf);

    //cmd_display();
}

/**************************************************************************/
/*!
    This function processes the individual characters typed into the command
    prompt. It saves them off into the message buffer unless its a "backspace"
    or "enter" key.
*/
/**************************************************************************/
void cmd_handler()
{
    char c = stream->read();

    switch (c)
    {
    case '.':
    case '\r':
        // terminate the msg and reset the msg ptr. then send
        // it to the handler for processing.
        *msg_ptr = '\0';
        if( echo_ctrl )
          stream->print("\r\n");
        cmd_parse((char *)msg);
        msg_ptr = msg;
        break;

    case '\b':
        // backspace
        if( echo_ctrl )
          stream->print(c);
        if (msg_ptr > msg)
        {
            msg_ptr--;
        }
        break;

    default:
        // normal character entered. add it to the buffer
        if( echo_ctrl )
          stream->print(c);
        *msg_ptr++ = c;
        break;
    }
 
}

/**************************************************************************/
/*!
    This function should be set inside the main loop. It needs to be called
    constantly to check if there is any available input at the command prompt.
*/
/**************************************************************************/
void cmdPoll()
{
    while (stream->available())
    {
        cmd_handler();
    }
}

/**************************************************************************/
/*!
    Initialize the command line interface. This sets the terminal speed and
    and initializes things.
*/
/**************************************************************************/
void cmdInit(Stream *str)
{
    stream = str;
    // init the msg ptr
    msg_ptr = msg;

    // init the command table
    cmd_tbl_list = NULL;

}

/**************************************************************************/
/*!
    Add a command to the command table. The commands should be added in
    at the setup() portion of the sketch.
*/
/**************************************************************************/
void cmdAdd(char *name, void (*func)(int argc, char **argv))
{
    // alloc memory for command struct
    cmd_tbl = (cmd_t *)malloc(sizeof(cmd_t));

    // alloc memory for command name
    char *cmd_name = (char *)malloc(strlen(name)+1);

    // copy command name
    strcpy(cmd_name, name);

    // terminate the command name
    cmd_name[strlen(name)] = '\0';

    // fill out structure
    cmd_tbl->cmd = cmd_name;
    cmd_tbl->func = func;
    cmd_tbl->next = cmd_tbl_list;
    cmd_tbl_list = cmd_tbl;
}

/**************************************************************************/
/*!
    Convert a string to a number. The base must be specified, ie: "32" is a
    different value in base 10 (decimal) and base 16 (hexadecimal).
*/
/**************************************************************************/
int cmdStr2Num(char *str, uint8_t base, uint16_t *val)
{
int   i=0;
char  *tmp;

    if( base == 16 )            /* only two digit hex nember are allowed */
      if( strlen(str) != 2 )
        return -1;
        
    tmp = str;
    while( *tmp!='\0' && i<8 )
    {
      /* check if valid decimal number */
      if( base == 10 )
        if( !isDigit(*tmp) )
          return -1;
      /* check if valid hexadecimal number */
      if( base == 16 )
        if( !isHexadecimalDigit(*tmp) )
          return -1;          
      tmp++;
      i++;
    }
    *val = strtol(str, NULL, base);
    return 0;
}

