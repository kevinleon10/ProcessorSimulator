using System;
using System.Linq;
using System.Text;
using ProcessorSimulator.common;

namespace ProcessorSimulator.block
{
    public class Block<T>
    {
        public Block()
        {
            Words = new T[Constants.WordsInBlock];
        }

        public Block(T[] words)
        {
            Words = words;
        }

        public T[] Words { get; set; }

        public T GetValue(int index)
        {
            T wordData = Words[index];
            return wordData;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder = Words.Aggregate(builder, (current, t) => current.Append(" " + t + " "));
            return builder.ToString();
        }
    }
}