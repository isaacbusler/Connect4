/*
This module takes the given connect-4 board and makes the
correct evaluation given the piece values their positions
in the board. The evaluation value is increased by 3 if
the given AI piece is in the middle, increased 10 if there
are two in a row, 100 if three in a row, and 100000 if four
in a row. The evaluation value is deceased by the same amount
for Human pieces.
(apoligize for some of the lack of readability due to
the board just being a bit stream. will work on making it
more readable in the future)
*/

module Evaluation(
	input clk, //fpga clk
	input reset, //clk from rpi
	input rpi_data_stable, //1 if data received from pi is stable, 0 otherwise
	input [83:0] board, //connect4 board that is to be evaluated
	input enable, //enables or keeps the module disabled depending on if it's needed
	output reg evaluation_stable, //determines if the evaluation is finished
	output reg signed [31:0] evaluation //evaluation that is calculated
);

	//constants
	localparam EMPTY = 2'd0; //position value if empty
	localparam AI = 2'd1; //position value if ai piece
	localparam HUMAN = 2'd2; //position value if human piece
	localparam OUTOFBOUNDS = 2'd3; //position value if invalid for evaluation
	localparam ROWS = 3'd6;
	localparam COLS = 3'd7;
	localparam bits_per_board = 7'd84;
	localparam bits_sent_per_board = 7'd88;
	
	//state related
	reg [3:0] state;
	localparam IDLE = 4'd0;
	localparam EVAL_CENTER = 4'd1;
	localparam GET_HORIZONTAL_LOAD = 4'd2;
	localparam GET_HORIZONTAL_NEXT = 4'd3;
	localparam GET_VERTICAL_LOAD = 4'd4;
	localparam GET_VERTICAL_NEXT = 4'd5;
	localparam GET_DIAGONAL1_LOAD = 4'd6;
	localparam GET_DIAGONAL1_NEXT = 4'd7;
	localparam GET_DIAGONAL2_LOAD = 4'd8;
	localparam GET_DIAGONAL2_NEXT = 4'd9;
	localparam CALC = 4'd10;
	localparam DONE = 4'd11;
	
	reg [3:0] return_state; //state that should be returned to after going to CALC
	reg [1:0] p0; //first board position
	reg [1:0] p1; //second board position
	reg [1:0] p2; //third board position
	reg [1:0] p3; //fourth board position
	reg [9:0] current_index; //used mostly to move the position in the board horizontally
	reg [5:0] get_horizontal_multiply; //used mostly to move the position in the board vertically
	
	

	always @(posedge clk) begin
		//resets everything to a known state
		if (!reset)	begin
			evaluation_stable <= 0;
			evaluation <= 0;
			state <= IDLE;
			return_state <= IDLE;
			p0 <= 2'd0;
			p1 <= 2'd0;
			p2 <= 2'd0;
			p3 <= 2'd0;
			current_index <= 10'd0;
			get_horizontal_multiply <= 6'd0;
		end
		
		//executes when the rpi data is stable and this module is enabled
		else if (rpi_data_stable && enable) begin
			case (state)
				IDLE: begin
					 evaluation_stable <= 0; //output not valid yet
					 evaluation <= 0; //reset evaluation score
					 current_index <= 6; //reset index to start of board
					 get_horizontal_multiply <= 1; //reset multiplier
					 p0 <= 0; p1 <= 0; p2 <= 0; p3 <= 0;  //rest board positions
					 if (enable && rpi_data_stable) begin
						  state <= EVAL_CENTER; //move to first evaluation step
					 end
					 else begin
						  state <= IDLE; //stay in idle until ready
					 end
				end

				//looks at all the positions in the middle row and adds 3 if a position is has an AI piece and subtracts 3 if a position has a HUMAN piece
				//basically makes the middle more desirable by giving it more value
				EVAL_CENTER: begin
					if (current_index <= bits_per_board - 1) begin
						if (((board >> current_index) & OUTOFBOUNDS) == AI) begin
							evaluation <= evaluation + 3;
						end
						else if (((board >> current_index) & OUTOFBOUNDS) == HUMAN) begin
							evaluation <= evaluation - 3;
						end
						current_index <= current_index + 2 * COLS;
					end
					else begin
						current_index <= 0;
						state <= GET_HORIZONTAL_LOAD;
					end
				end
				
				//gets the position at the starting index and thee positions adjacent horizontally
				//starting index increments each time and moves down a row when it reaches the last col
				GET_HORIZONTAL_LOAD: begin
					p0 <= (board >> current_index) & 2'b11;
					
					if (!(current_index + 3 > 2 * COLS * get_horizontal_multiply - 1)) begin
						p1 <= (board >> (current_index + 2)) & 2'b11;
					end
					else p1 <= OUTOFBOUNDS;
					if (!(current_index + 5 > 2 * COLS * get_horizontal_multiply - 1)) begin
						p2 <= (board >> (current_index + 4)) & 2'b11;
					end
					else p2 <= OUTOFBOUNDS;
					if (!(current_index + 7 > 2 * COLS * get_horizontal_multiply - 1)) begin
						p3 <= (board >> (current_index + 6)) & 2'b11;
					end
					else p3 <= OUTOFBOUNDS;
					return_state <= GET_HORIZONTAL_NEXT;
					state <= CALC;
				end
				
				//checks to see if the current index is beyond the range of the board
				//if it is it moves on to the next state, otherwise it calculates that part of the evaluation
				//increments current_index to next position and get_horizontal_multiply if it needs to move down a row
				GET_HORIZONTAL_NEXT: begin
					current_index <= current_index + 2;
					if (current_index > bits_per_board - 1) begin
						state <= GET_VERTICAL_LOAD;
						current_index <= 0;
						get_horizontal_multiply <= 0;
					end
					else if (((current_index + 2) % (2 * COLS) == 0) && (current_index != 0)) begin //current_index - 1? or 2?
						get_horizontal_multiply <= get_horizontal_multiply + 1;
						state <= GET_HORIZONTAL_LOAD;
					end
					else state <= GET_HORIZONTAL_LOAD;
				
				end
				
				//gets the position at the starting index and thee positions adjacent vertically
				//get_horizontal multiply increments each time to move down the col and current_index increases when the end of the col is reached
				GET_VERTICAL_LOAD: begin
					p0 <= (board >> (2 * COLS * get_horizontal_multiply + current_index)) & 2'b11;

					if (!(2 * COLS * (get_horizontal_multiply + 1) + current_index + 1 > bits_per_board - 1)) begin
						p1 <= (board >> (2 * COLS * (get_horizontal_multiply + 1) + current_index)) & 2'b11;

					end
					else p1 <= OUTOFBOUNDS;
					if (!(2 * COLS * (get_horizontal_multiply + 2) + current_index + 1 > bits_per_board - 1)) begin
						p2 <= (board >> (2 * COLS * (get_horizontal_multiply + 2) + current_index)) & 2'b11;

					end
					else p2 <= OUTOFBOUNDS;
					if (!(2 * COLS * (get_horizontal_multiply + 3) + current_index + 1 > bits_per_board - 1)) begin
						p3 <= (board >> (2 * COLS * (get_horizontal_multiply + 3) + current_index)) & 2'b11;

					end
					else p3 <= OUTOFBOUNDS;
					return_state <= GET_VERTICAL_NEXT;
					state <= CALC;
				end
			
				//checks to see if the current position is beyond the range of the board
				//if it is it moves on to the next state, otherwise it calculates that part of the evaluation
				//increments get_horizontal_multiply to next position and current_index if it needs to move down a col
				GET_VERTICAL_NEXT: begin
					get_horizontal_multiply <= get_horizontal_multiply + 1;
					if (current_index == 2 * COLS - 2 && get_horizontal_multiply == ROWS) begin
						state <= GET_DIAGONAL1_LOAD;
						current_index <= 0;
						get_horizontal_multiply <= 0;
					
					end
					
					else if (get_horizontal_multiply * 2 * COLS > bits_per_board - 1) begin
						get_horizontal_multiply <= 0;
						current_index <= current_index + 2;
						state <= GET_VERTICAL_LOAD;
					
					end
					else state <= GET_VERTICAL_LOAD;
					
				end
				
				//gets the position at the starting index and thee positions adjacent diagonally one way
				//get_horizontal multiply increments each time to move down the col and current_index increases when the end of the col is reached
				GET_DIAGONAL1_LOAD: begin
					p0 <= (board >> (2 * COLS * get_horizontal_multiply + current_index)) & 2'b11;

					if (!((2 * COLS * (get_horizontal_multiply + 1) + current_index + 2 + 1 > bits_per_board - 1) || 
						(current_index + 2 + 1 > 2 * COLS - 1))) begin
						p1 <= (board >> (2 * COLS * (get_horizontal_multiply + 1) + current_index + 2)) & 2'b11;

					end
					else p1 <= OUTOFBOUNDS;
					if (!((2 * COLS * (get_horizontal_multiply + 2) + current_index + 4 + 1 > bits_per_board - 1) ||
						(current_index + 4 + 1 > 2 * COLS - 1))) begin
						p2 <= (board >> (2 * COLS * (get_horizontal_multiply + 2) + current_index + 4)) & 2'b11;

					end
					else p2 <= OUTOFBOUNDS;
					if (!((2 * COLS * (get_horizontal_multiply + 3) + current_index + 6 + 1 > bits_per_board - 1) ||
						(current_index + 6 + 1 > 2 * COLS - 1))) begin
						p3 <= (board >> (2 * COLS * (get_horizontal_multiply + 3) + current_index + 6)) & 2'b11;

					end
					else p3 <= OUTOFBOUNDS;
					return_state <= GET_DIAGONAL1_NEXT;
					state <= CALC;
				end
				
				//checks to see if the current position is beyond the range of the board
				//if it is it moves on to the next state, otherwise it calculates that part of the evaluation
				//increments get_horizontal_multiply to next position and current_index if it needs to move down a col
				GET_DIAGONAL1_NEXT: begin
					get_horizontal_multiply <= get_horizontal_multiply + 1;
					if (current_index == 2 * COLS - 2 && get_horizontal_multiply == ROWS) begin
						state <= GET_DIAGONAL2_LOAD;
						current_index <= 0;
						get_horizontal_multiply <= 0;
					end
					else if (get_horizontal_multiply * 2 * COLS > bits_per_board - 1) begin
						get_horizontal_multiply <= 0;
						current_index <= current_index + 2;
						state <= GET_DIAGONAL1_LOAD;
					
					end
					else state <= GET_DIAGONAL1_LOAD;
				end
				
				//gets the position at the starting index and thee positions adjacent diagonally the other way
				//get_horizontal multiply increments each time to move down the col and current_index increases when the end of the col is reached
				GET_DIAGONAL2_LOAD: begin
					p0 <= (board >> (2 * COLS * get_horizontal_multiply + current_index)) & 2'b11;
					
					if (!((2 * COLS * (get_horizontal_multiply + 1) + current_index - 2 + 1 > bits_per_board - 1) || 
						(current_index < 2))) begin
						p1 <= (board >> (2 * COLS * (get_horizontal_multiply + 1) + current_index - 2)) & 2'b11;

					end
					else p1 <= OUTOFBOUNDS;
					if (!((2 * COLS * (get_horizontal_multiply + 2) + current_index - 4 + 1 > bits_per_board - 1) ||
						(current_index < 4))) begin
						p2 <= (board >> (2 * COLS * (get_horizontal_multiply + 2) + current_index - 4)) & 2'b11;

					end
					else p2 <= OUTOFBOUNDS;
					if (!((2 * COLS * (get_horizontal_multiply + 3) + current_index - 6 + 1 > bits_per_board - 1) ||
						(current_index < 6))) begin
						p3 <= (board >> (2 * COLS * (get_horizontal_multiply + 3) + current_index - 6)) & 2'b11;

					end
					else p3 <= OUTOFBOUNDS;
					return_state <= GET_DIAGONAL2_NEXT;
					state <= CALC;
				end
				
				//checks to see if the current position is beyond the range of the board
				//if it is it moves on to the next state, otherwise it calculates that part of the evaluation
				//increments get_horizontal_multiply to next position and current_index if it needs to move down a col
				GET_DIAGONAL2_NEXT: begin
					get_horizontal_multiply <= get_horizontal_multiply + 1;
					if (current_index == 2 * COLS - 2 && get_horizontal_multiply == ROWS) begin
						state <= DONE;
						current_index <= 0;
						get_horizontal_multiply <= 0;
					end
					else if (get_horizontal_multiply * 2 * COLS > bits_per_board - 1) begin
						get_horizontal_multiply <= 0;
						current_index <= current_index + 2;
						state <= GET_DIAGONAL2_LOAD;
					
					end
					else state <= GET_DIAGONAL2_LOAD;
				
				end
				
				//makes an evaluation of the four pieces that are passed to it from LOAD states and adds that to the current evaluation
				CALC: begin
					evaluation <= evaluation + evaluation_window(p0, p1, p2, p3);
					state <= return_state;
				end
				
				//sets evaluation as stable
				DONE: begin
					evaluation_stable <= 1;
					current_index <= 0;
					get_horizontal_multiply <= 0;
					state <= IDLE;
				
				end
				
				default:
					state <= IDLE;
				
			endcase
			
		end
		else state <= IDLE;

	end
	
	//makes an evaluation of the four pieces that are passed to it
   //score 10 or -10 if two in a row depending if AI or HUMAN
	//score 100 or -100 if three in a row depending if AI or HUMAN
	//score 100000 or -100000 if four in a row depending if AI or HUMAN
	//positive if AI, negative if HUMAN
	function integer evaluation_window;
		input [1:0] w0;
		input [1:0] w1;
		input [1:0] w2;
		input [1:0] w3;
		
		integer empty, ai, human;
		
		begin
			empty = 0;
			ai = 0;
			human = 0;
				
			case (w0)
				EMPTY: empty = empty + 1;
				AI: ai = ai + 1;
				HUMAN: human = human + 1;
			endcase
			case (w1)
				EMPTY: empty = empty + 1;
				AI: ai = ai + 1;
				HUMAN: human = human + 1;
			endcase
			case (w2)
				EMPTY: empty = empty + 1;
				AI: ai = ai + 1;
				HUMAN: human = human + 1;
			endcase
			case (w3)
				EMPTY: empty = empty + 1;
				AI: ai = ai + 1;
				HUMAN: human = human + 1;
			endcase
				
			if (ai == 4) evaluation_window = 100000;
			else if (human == 4) evaluation_window = -100000;
			else if (ai== 3 && empty == 1) evaluation_window = 100;
			else if (human == 3 && empty == 1) evaluation_window = -100;
			else if (ai == 2 && empty == 2) evaluation_window = 10;
			else if (human == 2 && empty == 2) evaluation_window = -10;
			else evaluation_window = 0;
		
		end
	endfunction
	
endmodule
