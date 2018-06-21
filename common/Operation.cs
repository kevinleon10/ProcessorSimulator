namespace ProcessorSimulator.common
{
    public enum Operation
    {
        JR = 2, 
        JAL = 3, 
        BEQZ = 4, 
        BNEZ = 5, 
        DADDI = 8, 
        DMUL = 12, 
        DDIV = 14, 
        DADD = 32, 
        DSUB = 34, 
        LW = 35, 
        SW = 43, 
        END = 63 
    }
}