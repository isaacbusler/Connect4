`timescale 1ns/1ps

module Evaluation_tb();

reg clk;
reg reset;
reg rpi_data_stable;
reg [83:0] rpi_batch;
wire evaluation_stable;
wire signed [31:0] evaluation;
reg enable;

initial begin
	clk <= 1'd0;
	forever #5 clk <= ~clk;
end

Evaluation uut (
	.clk(clk),
	.reset(reset),
	.rpi_data_stable(rpi_data_stable),
	.board(rpi_batch), //.board(rpi_batch[(615 - (88 * 6) - 4) -: 84]),
	.evaluation_stable(evaluation_stable),
	.evaluation(evaluation),
	.enable(enable)
);


initial begin
	reset = 0; //reset evaluation instance
	enable <= 1; //sets enable to 1
	
	

rpi_batch <= {
	//84'b000000000000000000000000000001000000000000100000000000001000000000000001010010000000
	84'b000000000000000000000000000000000000000000100000000000001001000000000001010010000000
};
	
	#20 reset = 1;  //release reset
	#10 rpi_data_stable <= 1; //rpi data is set as stable
	

	//waits for the evaluation to be stable to display the final value
	wait (evaluation_stable);
	#10; //one more cycle to settle
	$display("Board[%0d]: %b", 6, rpi_batch);
	$display("Final Evaluation = %d", evaluation);
	$stop;
	
end


initial begin
	$monitor("Time: %0t | state: %0d | evaluation: %0d | stable: %b | %b %b %b %b",
	         $time, uut.state, evaluation, evaluation_stable, uut.p0, uut.p1, uut.p2, uut.p3);
end


endmodule