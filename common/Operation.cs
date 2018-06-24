namespace ProcessorSimulator.common
{
    public enum Operation
    {
        Jr = 2, 
        Jal = 3, 
        Beqz = 4, 
        Bnez = 5, 
        Daddi = 8, 
        Dmul = 12, 
        Ddiv = 14, 
        Dadd = 32, 
        Dsub = 34, 
        Lw = 35, 
        Sw = 43, 
        End = 63 
    }
}